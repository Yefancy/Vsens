using UnityEngine;
using NativeWebSocket;
using System.Text;
using System;
using System.Threading.Tasks;

public class WsClient : MonoBehaviour
{
    private static WebSocket websocket;

    // 事件定义
    public static event Action<string> OnTTSAudioReady;
    public static event Action<BehaviorMessage> OnBehaviorCommand;

    async void Start()
    {
        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () => Debug.Log("[WS] Connection opened.");
        websocket.OnError += (e) => Debug.LogError("[WS] Error: " + e);
        websocket.OnClose += (e) => Debug.Log("[WS] Connection closed.");

        websocket.OnMessage += (bytes) =>
        {
            string json = Encoding.UTF8.GetString(bytes);
            Debug.Log("[WS] Message: " + json);

            try
            {
                var typeWrapper = JsonUtility.FromJson<MessageTypeWrapper>(json);

                switch (typeWrapper.type)
                {
                    case "tts_audio_ready":
                        var ttsMsg = JsonUtility.FromJson<TTSMessage>(json);
                        OnTTSAudioReady?.Invoke(ttsMsg.file);
                        break;

                    case "behavior":
                        var behaviorMsg = JsonUtility.FromJson<BehaviorMessage>(json);
                        OnBehaviorCommand?.Invoke(behaviorMsg);
                        break;

                    default:
                        Debug.LogWarning($"[WS] Unknown message type: {typeWrapper.type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[WS] Failed to parse message: " + ex.Message);
            }
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    public static void SendTranscribeRequest(string audioPath)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            var payload = new TranscribeRequest()
            {
                type = "transcribe_and_reply",
                audio_path = audioPath
            };

            string json = JsonUtility.ToJson(payload);
            websocket.SendText(json);
            Debug.Log("[WS] Sent transcribe request: " + json);
        }
        else
        {
            Debug.LogWarning("[WS] WebSocket not connected, cannot send audio path.");
        }
    }

    [Serializable]
    public class TranscribeRequest
    {
        public string type;
        public string audio_path;
    }

    private async void OnApplicationQuit()
    {
        await websocket.Close();
    }

    // 类型封装类
    [Serializable]
    public class MessageTypeWrapper
    {
        public string type;
    }

    [Serializable]
    public class TTSMessage
    {
        public string type;
        public string file;
    }

    [Serializable]
    public class BehaviorMessage
    {
        public string type;
        public string action;
        public string target;
        public string emotion;
    }
}
