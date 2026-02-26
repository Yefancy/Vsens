using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using VsensAgent.Agent;
using VsensAgent.Network.Protocol;
using VsensAgent.Core;

namespace VsensAgent.Network
{
    public class WsClient : MonoBehaviour
    {
    private static WebSocket websocket;
    private static bool isTryingReconnect = false;
    private static float reconnectInterval = Constants.Network.RECONNECT_INTERVAL;

    // Agent行为控制器引用
    public AgentBehaviorController agentBehaviorController;
    
    // 事件定义
    public static event Action<AgentReplyMessage> OnAgentReply;  // 统一的Agent回复事件（语音+文字）
    public static event Action<ControlObject[]> OnControl; // 统一使用数组
    public static event Action<AgentBehavior> OnAgentBehavior;

    async void Start()
    {
        // 注册到服务定位器
        ServiceLocator.Register<WsClient>(this);
        
        await ConnectWebSocket();
    }

    async Task ConnectWebSocket()
    {
        websocket = new WebSocket(Constants.Network.WS_SERVER_URL);

        websocket.OnOpen += () =>
        {
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
            yield return new WaitForSeconds(reconnectInterval);

            Task connectTask = ConnectWebSocket();
            while (!connectTask.IsCompleted) yield return null;

            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                isTryingReconnect = false;
                break;
            }
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            var typeWrapper = JsonConvert.DeserializeObject<MessageTypeWrapper>(json);

            switch (typeWrapper.type)
            {
                case "agent_ready":
                    var replyMsg = JsonConvert.DeserializeObject<AgentReplyMessage>(json);
                    
                    // 触发统一的回复事件
                    OnAgentReply?.Invoke(replyMsg);
                    
                    // 停止思考状态 - 结束呼吸动画
                    var wsClient = ServiceLocator.Get<WsClient>();
                    if (wsClient != null && wsClient.agentBehaviorController != null)
                    {
                        wsClient.agentBehaviorController.StopThinking();
                    }
                    
                    // 处理控制指令
                    if (replyMsg.control != null && replyMsg.control.actions != null)
                    {
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

    public static void SendTranscribeRequest(string audioPath)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            // 获取场景描述
            string sceneDescription = GetCurrentSceneDescription();
            
            var payload = new TranscribeRequest()
            {
                type = "transcribe_and_reply",
                audio_path = audioPath,
                scene_snapshot = sceneDescription,
            };

            string json = JsonConvert.SerializeObject(payload);
            websocket.SendText(json);
            
            // 开始思考状态 - 启动呼吸动画
            var wsClient = ServiceLocator.Get<WsClient>();
            if (wsClient != null && wsClient.agentBehaviorController != null)
            {
                wsClient.agentBehaviorController.StartThinking();
            }
        }
        else
        {
            Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send audio path.");
        }
    }

    public static void ClearHistory()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            var payload = new ResetRequest()
            {
                type = "reset",
            };

            string json = JsonConvert.SerializeObject(payload);
            websocket.SendText(json);
            
            var wsClient = ServiceLocator.Get<WsClient>();
            if (wsClient != null)
            {
                // 开始思考状态 - 启动呼吸动画，2秒后自动停止
                if (wsClient.agentBehaviorController != null)
                {
                    wsClient.agentBehaviorController.StartThinking();
                    wsClient.agentBehaviorController.StopThinkingAfterDelay(2f);
                }
                
                // 清空聊天历史
                var chatUI = ServiceLocator.Get<VsensAgent.UI.ChatUIManager>();
                if (chatUI != null)
                {
                    chatUI.ClearChatHistory();
                }
            }
        }
        else
        {
            Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send text message.");
        }
    }

    public static void SendTextChatRequest(string message)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            // 获取场景描述
            string sceneDescription = GetCurrentSceneDescription();
            
            var payload = new TextChatRequest()
            {
                type = "text_chat",
                message = message,
                scene_snapshot = sceneDescription,
                request_audio = false
            };

            string json = JsonConvert.SerializeObject(payload);
            websocket.SendText(json);
            
            // 开始思考状态 - 启动呼吸动画
            var wsClient = ServiceLocator.Get<WsClient>();
            if (wsClient != null && wsClient.agentBehaviorController != null)
            {
                wsClient.agentBehaviorController.StartThinking();
            }
        }
        else
        {
            Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send text message.");
        }
    }

    public static bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;

    /// <summary>
    /// 获取当前场景描述的统一方法
    /// </summary>
    public static string GetCurrentSceneDescription()
    {
        RoomDescriber describer = FindFirstObjectByType<RoomDescriber>();
        if (describer != null)
        {
            return describer.GetRoomDescription().ToString();
        }
        else
        {
            Debug.LogWarning("[WsClient] No RoomDescriber found in scene.");
            return "{}";
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }
}
}
