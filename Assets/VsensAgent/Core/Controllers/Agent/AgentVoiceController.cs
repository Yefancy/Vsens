using UnityEngine;
using VsensAgent.Audio;
using VsensAgent.Core;

namespace VsensAgent.Agent
{
    /// <summary>
    /// Thin compatibility wrapper kept so that existing prefab references remain valid.
    /// All audio logic has moved to <see cref="AgentAudioManager"/> (VsensAgent/Audio/).
    /// AgentAudioManager subscribes to OnAgentReply and OnAgentPush directly —
    /// this component no longer needs to do anything on its own.
    /// </summary>
    public class AgentVoiceController : MonoBehaviour
    {
        // Kept for prefab Inspector compatibility; not used directly.
        [HideInInspector]
        public AudioSource audioSource;

        /// <summary>
        /// Delegates manual TTS playback to AgentAudioManager (e.g. called from tests
        /// or Editor tooling). Uses the queue so it never collides with live audio.
        /// </summary>
        public void PlayTTSFromPath(string path)
        {
            var mgr = ServiceLocator.Get<AgentAudioManager>();
            if (mgr != null)
                mgr.PlayNow(path);
            else
                Debug.LogWarning("[AgentVoiceController] AgentAudioManager not found in ServiceLocator.");
        }
    }
}

