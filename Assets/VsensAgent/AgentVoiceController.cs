using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class AgentVoiceController : MonoBehaviour
{
    public AudioSource audioSource;
    void OnEnable()
    {
        WsClient.OnAgentReply += OnAgentReplyReceived;
    }

    void OnDisable()
    {
        WsClient.OnAgentReply -= OnAgentReplyReceived;
    }

    /// <summary>
    /// 处理Agent回复，如果有音频则播放
    /// </summary>
    private void OnAgentReplyReceived(WsClient.AgentReplyMessage reply)
    {
        // 只有当有音频路径时才播放
        if (!string.IsNullOrEmpty(reply.audio_path))
        {
            PlayTTSFromPath(reply.audio_path);
        }
    }

    public void PlayTTSFromPath(string path)
    {
        StartCoroutine(LoadAndPlay(path));
    }

    private System.Collections.IEnumerator LoadAndPlay(string path)
    {
        // 将相对路径转换为绝对路径
        string absolutePath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);

        if (!File.Exists(absolutePath))
        {
            Debug.LogError($"[TTS] File not found: {absolutePath}");
            yield break;
        }

        string url = "file://" + absolutePath;
        AudioType audioType = GetAudioType(Path.GetExtension(absolutePath));

        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[TTS] Load error: " + request.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    private AudioType GetAudioType(string extension)
    {
        switch (extension.ToLower())
        {
            case ".wav":
                return AudioType.WAV;
            case ".mp3":
                return AudioType.MPEG;
            case ".ogg":
                return AudioType.OGGVORBIS;
            default:
                return AudioType.UNKNOWN;
        }
    }
}
