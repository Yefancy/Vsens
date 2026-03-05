using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using VsensAgent.Core;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;

namespace VsensAgent.Audio
{
    /// <summary>
    /// Central audio manager for all agent TTS playback.
    ///
    /// Responsibilities:
    ///   - Auto-plays audio from direct agent replies (OnAgentReply).
    ///   - Auto-plays audio from proactive agent pushes (OnAgentPush).
    ///   - Queues push audio when a clip is already playing, then auto-dequeues.
    ///   - Exposes PlayNow() for manual re-play (chat UI play button).
    ///
    /// Queue policy:
    ///   - OnAgentReply audio plays immediately (user-triggered, highest priority).
    ///     If a push clip is in progress it is interrupted so the reply is heard at once.
    ///   - OnAgentPush audio is enqueued when the AudioSource is busy and
    ///     dequeued automatically as soon as the current clip finishes.
    ///   - Manual PlayNow() (re-play button) also queues if something is playing.
    /// </summary>
    public class AgentAudioManager : MonoBehaviour
    {
        [Header("Audio")]
        public AudioSource audioSource;

        // Queue of audio file paths waiting to play (push messages accumulated while busy).
        private readonly Queue<string> _pushQueue = new Queue<string>();

        // Whether a LoadAndPlay coroutine is currently running.
        private bool _isCoroutineRunning = false;

        // -----------------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------------

        void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                Debug.LogWarning("[AgentAudio] AudioSource not assigned — created automatically.");
            }

            ServiceLocator.Register<AgentAudioManager>(this);
        }

        void OnEnable()
        {
            WsClient.OnAgentReply += HandleAgentReply;
            WsClient.OnAgentPush  += HandleAgentPush;
        }

        void OnDisable()
        {
            WsClient.OnAgentReply -= HandleAgentReply;
            WsClient.OnAgentPush  -= HandleAgentPush;
        }

        // -----------------------------------------------------------------------
        // Event handlers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Called when the agent replies to a direct user request.
        /// Plays immediately — interrupts any ongoing push clip so the user's
        /// own conversation is never blocked.
        /// </summary>
        private void HandleAgentReply(AgentReplyMessage reply)
        {
            if (string.IsNullOrEmpty(reply.audio_path)) return;
            PlayImmediately(reply.audio_path);
        }

        /// <summary>
        /// Called when a proactive heartbeat push arrives with audio.
        /// Queues the clip if something is already playing.
        /// </summary>
        private void HandleAgentPush(AgentPushMessage push)
        {
            if (string.IsNullOrEmpty(push.audio_path)) return;
            EnqueueOrPlay(push.audio_path);
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Re-play a stored audio path from the chat UI play button.
        /// Behaves like a push: queues if busy, plays immediately if idle.
        /// </summary>
        public void PlayNow(string audioPath)
        {
            if (string.IsNullOrEmpty(audioPath)) return;
            EnqueueOrPlay(audioPath);
        }

        // -----------------------------------------------------------------------
        // Internal playback logic
        // -----------------------------------------------------------------------

        /// <summary>
        /// Stops any current clip, clears the push queue, and plays the given
        /// path right away.  Used for OnAgentReply (highest priority).
        /// </summary>
        private void PlayImmediately(string audioPath)
        {
            StopAllCoroutines();
            audioSource.Stop();
            _pushQueue.Clear();
            _isCoroutineRunning = false;
            StartCoroutine(LoadAndPlay(audioPath, isFromQueue: false));
        }

        /// <summary>
        /// If the AudioSource is idle, play immediately.
        /// Otherwise enqueue and let the running coroutine pick it up.
        /// </summary>
        private void EnqueueOrPlay(string audioPath)
        {
            if (audioSource.isPlaying || _isCoroutineRunning)
            {
                _pushQueue.Enqueue(audioPath);
                Debug.Log($"[AgentAudio] Queued push audio (queue depth: {_pushQueue.Count}).");
            }
            else
            {
                StartCoroutine(LoadAndPlay(audioPath, isFromQueue: true));
            }
        }

        /// <summary>
        /// Loads an audio file from disk, plays it, then dequeues the next
        /// pending push clip (if any).
        /// </summary>
        private IEnumerator LoadAndPlay(string path, bool isFromQueue)
        {
            _isCoroutineRunning = true;

            string absolutePath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);

            if (!File.Exists(absolutePath))
            {
                Debug.LogError($"[AgentAudio] File not found: {absolutePath}");
                _isCoroutineRunning = false;
                TryDequeue();
                yield break;
            }

            string url = "file://" + absolutePath;
            AudioType audioType = GetAudioType(Path.GetExtension(absolutePath));

            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"[AgentAudio] Load error: {request.error}");
                    _isCoroutineRunning = false;
                    TryDequeue();
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                audioSource.clip = clip;
                audioSource.Play();

                // Wait for playback to finish.
                yield return new WaitWhile(() => audioSource.isPlaying);
            }

            _isCoroutineRunning = false;

            // Play the next queued push clip if available.
            TryDequeue();
        }

        /// <summary>
        /// Dequeues and plays the next push audio path, if any.
        /// </summary>
        private void TryDequeue()
        {
            if (_pushQueue.Count > 0)
            {
                string next = _pushQueue.Dequeue();
                Debug.Log($"[AgentAudio] Dequeuing next push audio (remaining: {_pushQueue.Count}).");
                StartCoroutine(LoadAndPlay(next, isFromQueue: true));
            }
        }

        private static AudioType GetAudioType(string extension)
        {
            switch (extension.ToLower())
            {
                case ".wav":  return AudioType.WAV;
                case ".mp3":  return AudioType.MPEG;
                case ".ogg":  return AudioType.OGGVORBIS;
                default:      return AudioType.UNKNOWN;
            }
        }
    }
}
