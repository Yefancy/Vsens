using UnityEngine;

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
        string url = "file://" + path;

        using (var www = new WWW(url))
        {
            yield return www;

            if (!string.IsNullOrEmpty(www.error))
            {
                Debug.LogError("[TTS] Load error: " + www.error);
                yield break;
            }

            AudioClip clip = www.GetAudioClip(false, false);
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}
