using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

public class WsClient : MonoBehaviour
{
    private static WebSocket websocket;
    private static bool isTryingReconnect = false;
    private static float reconnectInterval = 3f;

    // 事件定义
    public static event Action<string> OnAgentSpeechAudio;
    public static event Action<string> OnAgentSpeechText;
    public static event Action<ControlObject[]> OnControl; // 统一使用数组
    public static event Action<AgentBehavior> OnAgentBehavior;

    async void Start()
    {
        await ConnectWebSocket();
    }

    async Task ConnectWebSocket()
    {
        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () =>
        {
            Debug.Log("[WS] ✅ Connected.");
            isTryingReconnect = false;
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("[WS] ❌ Error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.LogWarning("[WS] ⚠️ Connection closed. Attempting reconnect...");
            TryReconnect();
        };

        websocket.OnMessage += (bytes) =>
        {
            string json = Encoding.UTF8.GetString(bytes);
            HandleMessage(json);
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    private void TryReconnect()
    {
        if (isTryingReconnect) return;

        isTryingReconnect = true;
        StartCoroutine(ReconnectCoroutine());
    }

    private IEnumerator ReconnectCoroutine()
    {
        while (isTryingReconnect)
        {
            Debug.Log("[WS] 🔁 Trying to reconnect...");
            yield return new WaitForSeconds(reconnectInterval);

            Task connectTask = ConnectWebSocket();
            while (!connectTask.IsCompleted) yield return null;

            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                Debug.Log("[WS] ✅ Reconnected successfully.");
                isTryingReconnect = false;
                break;
            }
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            Debug.Log($"[WS] 📥 Received message: {json}"); // 添加调试日志
            var typeWrapper = JsonConvert.DeserializeObject<MessageTypeWrapper>(json);

            switch (typeWrapper.type)
            {
                case "agent_ready":
                    var replyMsg = JsonConvert.DeserializeObject<AgentReplyMessage>(json);
                    OnAgentSpeechAudio?.Invoke(replyMsg.audio_path);
                    OnAgentSpeechText?.Invoke(replyMsg.reply);
                    
                    if (replyMsg.control != null && replyMsg.control.actions != null)
                    {
                        Debug.Log($"[WS] 🎮 Triggering OnControl with {replyMsg.control.actions.Length} actions");
                        OnControl?.Invoke(replyMsg.control.actions);
                    }
                    break;

                case "agent_behavior":
                    var behaviorMsg = JsonConvert.DeserializeObject<AgentBehavior>(json);
                    OnAgentBehavior?.Invoke(behaviorMsg);
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
    }

    public static void SendTranscribeRequest(string audioPath, string sceneSnapshotJson)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            // 获取主摄像机信息
            var payload = new TranscribeRequest()
            {
                type = "transcribe_and_reply",
                audio_path = audioPath,
                scene_snapshot = sceneSnapshotJson,
            };

            string json = JsonConvert.SerializeObject(payload);
            websocket.SendText(json);
            Debug.Log("[WS] 📤 Sent transcribe request: " + json);
        }
        else
        {
            Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send audio path.");
        }
    }

    public static bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;

    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }

    // 数据结构定义
    [Serializable]
    public class MessageTypeWrapper
    {
        public string type;
    }

    [Serializable]
    public class AgentReplyMessage
    {
        public string type;
        public string status;
        public string transcription;
        public string reply;
        public string audio_path;
        [JsonProperty("control")]
        public ControlActions control; // 使用包装类来处理 actions 字段
    }

    [Serializable]
    public class ControlActions
    {
        [JsonProperty("actions")]
        public ControlObject[] actions;
        
        public ControlActions()
        {
            actions = new ControlObject[0];
        }
    }

    [Serializable]
    public class TranscribeRequest
    {
        public string type;
        public string audio_path;
        public string scene_snapshot;
    }

    [Serializable]
    public class ControlObject
    {
        public string target;
        public string action;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> parameters;
        
        public ControlObject()
        {
            parameters = new Dictionary<string, string>();
        }
    }

    [Serializable]
    public class AgentBehavior
    {
        public string type;
        public string action;
        public string target;
        public string emotion;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> parameters;
        
        public AgentBehavior()
        {
            parameters = new Dictionary<string, string>();
        }
    }
}
