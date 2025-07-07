using UnityEngine;

public class AgentVoiceController : MonoBehaviour
{
    public AudioSource audioSource;
    void OnEnable()
    {
        WsClient.OnAgentSpeechAudio += PlayTTSFromPath;
    }

    void OnDisable()
    {
        WsClient.OnAgentSpeechAudio -= PlayTTSFromPath;
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
