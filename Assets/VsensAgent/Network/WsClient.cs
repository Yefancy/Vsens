using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using Newtonsoft.Json;
using VsensAgent.Agent;
using VsensAgent.Network.Protocol;
using VsensAgent.Core;
using VsensAgent.SceneHistory;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using VsensAgent.RuntimeEditing;

namespace VsensAgent.Network
{
    public class WsClient : MonoBehaviour
    {
        private static WebSocket websocket;
        private static bool isTryingReconnect = false;
        private static float reconnectInterval = Constants.Network.RECONNECT_INTERVAL;
        private static readonly Queue<UnitySceneActionLogRequest> PendingSceneActionLogs = new();
        private const int MaxPendingSceneActionLogs = 512;

        // Agent行为控制器引用
        public AgentBehaviorController agentBehaviorController;

        private bool isConnecting = false;
        public bool IsConnecting => isConnecting;
        public string ClientId => clientId;
        public string Username => pendingUsername;
        public string Us => pendingUs;
        public bool HasIdentifiedSession => IsConnected && !string.IsNullOrWhiteSpace(clientId);
        
        private Action pendingOnConnected;
        private Action<string> pendingOnFailed;
        private string pendingUsername = string.Empty;
        private string pendingUs = string.Empty;
        private string clientId = string.Empty;

        [Header("Delegated Task Defaults")]
        [SerializeField] private int defaultRecentAnalysisCount = 3;
        [SerializeField] private string defaultAnalysisGoal = "Analyze the recent recordings and summarize the key findings.";

        
        // 事件定义
        public static event Action<AgentReplyMessage> OnAgentReply; // 统一的Agent回复事件（语音+文字）
        public static event Action<ControlObject[]> OnControl; // 统一使用数组
        public static event Action<AgentBehavior> OnAgentBehavior;
        public static event Action<AgentStatusMessage> OnAgentStatus; // Python端主动推送的Agent状态
        public static event Action<string> OnSceneApiRequest; // Scene API v2 request raw payload
        public static event Action<ConversationReplyMessage> OnConversationReply;
        public static event Action<ClarificationRequestMessage> OnClarificationRequest;
        public static event Action<ProposalReadyMessage> OnProposalReady;
        public static event Action<ObjectSelectionRequestMessage> OnObjectSelectionRequest;
        public static event Action<JobLifecycleMessage> OnJobLifecycle;
        public static event Action<DelegatedTaskResultMessage> OnDelegatedTaskResult;
        public static event Action<DelegatedTaskArtifactsSnapshotMessage> OnDelegatedTaskArtifactsSnapshot;
        public static event Action<AgentPushMessage> OnAgentPush; // Phase 3: 心跳触发的主动推送
        public static event Action<ServerConfigMessage> OnServerConfig; // Phase 3: 连接时接收服务器配置

        void Start()
        {
            // 注册到服务定位器
            ServiceLocator.Register<WsClient>(this);
        }

        public void RequestLatestDataAnalysis()
        {
            SendAnalysisTaskLatest(defaultAnalysisGoal);
        }

        public void RequestAllDataAnalysis()
        {
            SendAnalysisTaskAll(defaultAnalysisGoal);
        }

        public void RequestRecentDataAnalysis()
        {
            SendAnalysisTaskRecent(defaultRecentAnalysisCount, defaultAnalysisGoal);
        }

        public void ConnectToServer(string serverUrl = null, string username = null, string us = null, Action onConnected = null, Action<string> onFailed = null)
        {
            if (string.IsNullOrEmpty(serverUrl))
            {
                serverUrl = Constants.Network.WS_SERVER_URL;
            }

            var wsUrl = "ws://" + serverUrl;
            Debug.Log($"[WS] Connecting to {wsUrl}...");
            pendingUsername = username ?? string.Empty;
            pendingUs = us ?? string.Empty;
            pendingOnConnected = onConnected;
            pendingOnFailed = onFailed;

            _ = ConnectWebSocket(wsUrl, onConnected, onFailed);
        }


        private async Task ConnectWebSocket(
            string serverUrl,
            Action onConnected = null,
            Action<string> onFailed = null)
        {
            isConnecting = true;

            try
            {
                if (websocket != null)
                {
                    try
                    {
                        await websocket.Close();
                    }
                    catch
                    {
                    }

                    websocket = null;
                }

                websocket = new WebSocket(serverUrl);

                bool connected = false;

                websocket.OnOpen += () =>
                {
                    connected = true;
                    isConnecting = false;
                    isTryingReconnect = false;

                    Debug.Log("[WS] ✅ Connected.");
                    SendClientHello();
                };

                websocket.OnError += (e) =>
                {
                    isConnecting = false;
                    Debug.LogError("[WS] ❌ Error: " + e);
                    pendingOnFailed?.Invoke(e);
                };

                websocket.OnClose += (e) =>
                {
                    isConnecting = false;
                    Debug.LogWarning("[WS] ⚠️ Connection closed.");
                };

                websocket.OnMessage += (bytes) =>
                {
                    string json = Encoding.UTF8.GetString(bytes);
                    HandleMessage(json);
                };

                // ⭐ 关键：超时控制
                var connectTask = websocket.Connect();
                var timeoutTask = Task.Delay(10000); // 10秒

                var completed = await Task.WhenAny(connectTask, timeoutTask);

                if (completed == timeoutTask && !connected)
                {
                    Debug.LogError("[WS] ⏰ Connect timeout (10s)");

                    try
                    {
                        await websocket.Close();
                    }
                    catch
                    {
                    }

                    websocket = null;
                    isConnecting = false;

                    onFailed?.Invoke("Timeout (10s)");
                    return;
                }

                // 如果是 connectTask 完成，这里继续 await，捕获异常
                await connectTask;
            }
            catch (Exception ex)
            {
                isConnecting = false;
                Debug.LogError("[WS] ❌ Connect exception: " + ex);
                onFailed?.Invoke(ex.Message);
            }
        }

        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            websocket?.DispatchMessageQueue();
#endif
        }

        private void TryReconnect(string serverUrl)
        {
            if (isTryingReconnect) return;

            isTryingReconnect = true;
            StartCoroutine(ReconnectCoroutine(serverUrl));
        }

        private IEnumerator ReconnectCoroutine(string serverUrl)
        {
            while (isTryingReconnect)
            {
                yield return new WaitForSeconds(reconnectInterval);

                Task connectTask = ConnectWebSocket(serverUrl);
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
                if (typeWrapper == null || string.IsNullOrWhiteSpace(typeWrapper.type))
                {
                    Debug.LogWarning($"[WS] Ignoring message without a valid type: {json}");
                    return;
                }

                switch (typeWrapper.type)
                {
                    case "agent_ready":
                        var replyMsg = JsonConvert.DeserializeObject<AgentReplyMessage>(json);
                        if (replyMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize agent_ready payload: {json}");
                            break;
                        }

                        SafeInvoke(() => OnAgentReply?.Invoke(replyMsg), "agent_ready.OnAgentReply", json);
                        StopThinkingAnimation();

                        if (replyMsg.control != null && replyMsg.control.actions != null)
                        {
                            SafeInvoke(() => OnControl?.Invoke(replyMsg.control.actions), "agent_ready.OnControl",
                                json);
                        }

                        break;

                    case "conversation.reply":
                        var conversationMsg = JsonConvert.DeserializeObject<ConversationReplyMessage>(json);
                        if (conversationMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize conversation.reply payload: {json}");
                            break;
                        }

                        SafeInvoke(() => OnConversationReply?.Invoke(conversationMsg),
                            "conversation.reply.OnConversationReply", json);
                        SafeInvoke(() => OnAgentReply?.Invoke(ToAgentReplyMessage(
                            conversationMsg.type,
                            conversationMsg.status,
                            conversationMsg.transcription,
                            conversationMsg.reply,
                            conversationMsg.audio_path,
                            conversationMsg.control)), "conversation.reply.OnAgentReply", json);
                        StopThinkingAnimation();
                        if (conversationMsg.control != null && conversationMsg.control.actions != null)
                        {
                            SafeInvoke(() => OnControl?.Invoke(conversationMsg.control.actions),
                                "conversation.reply.OnControl", json);
                        }

                        break;

                    case "clarification.request":
                        var clarificationMsg = JsonConvert.DeserializeObject<ClarificationRequestMessage>(json);
                        if (clarificationMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize clarification.request payload: {json}");
                            break;
                        }

                        SafeInvoke(() => OnAgentReply?.Invoke(ToAgentReplyMessage(
                            clarificationMsg.type,
                            clarificationMsg.status,
                            clarificationMsg.transcription,
                            clarificationMsg.reply,
                            clarificationMsg.audio_path,
                            clarificationMsg.control)), "clarification.request.OnAgentReply", json);
                        StopThinkingAnimation();
                        SafeInvoke(() => OnClarificationRequest?.Invoke(clarificationMsg),
                            "clarification.request.OnClarificationRequest", json);
                        if (clarificationMsg.control != null && clarificationMsg.control.actions != null)
                        {
                            SafeInvoke(() => OnControl?.Invoke(clarificationMsg.control.actions),
                                "clarification.request.OnControl", json);
                        }

                        break;

                    case "proposal.ready":
                        var proposalMsg = JsonConvert.DeserializeObject<ProposalReadyMessage>(json);
                        if (proposalMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize proposal.ready payload: {json}");
                            break;
                        }

                        SafeInvoke(() => OnAgentReply?.Invoke(ToAgentReplyMessage(
                            proposalMsg.type,
                            proposalMsg.status,
                            proposalMsg.transcription,
                            proposalMsg.reply,
                            proposalMsg.audio_path,
                            proposalMsg.control)), "proposal.ready.OnAgentReply", json);
                        StopThinkingAnimation();
                        SafeInvoke(() => OnProposalReady?.Invoke(proposalMsg), "proposal.ready.OnProposalReady", json);
                        if (proposalMsg.control != null && proposalMsg.control.actions != null)
                        {
                            SafeInvoke(() => OnControl?.Invoke(proposalMsg.control.actions), "proposal.ready.OnControl",
                                json);
                        }

                        break;

                    case "object_selection.request":
                        var selectionMsg = JsonConvert.DeserializeObject<ObjectSelectionRequestMessage>(json);
                        if (selectionMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize object_selection.request payload: {json}");
                            break;
                        }

                        StopThinkingAnimation();
                        SafeInvoke(() => OnObjectSelectionRequest?.Invoke(selectionMsg),
                            "object_selection.request.OnObjectSelectionRequest", json);
                        EnsureObjectSelectionController()?.BeginSelection(selectionMsg);
                        break;

                    case "agent_behavior":
                        var behaviorMsg = JsonConvert.DeserializeObject<AgentBehavior>(json);
                        OnAgentBehavior?.Invoke(behaviorMsg);
                        break;

                    case "agent_status":
                        // Phase 1: Python端主动推送的Agent状态变更（无需Unity请求）
                        var statusMsg = JsonConvert.DeserializeObject<AgentStatusMessage>(json);
                        OnAgentStatus?.Invoke(statusMsg);
                        break;

                    case "interrupt_ack":
                        // Phase 1: 收到中断确认（agent_interrupt 或 plan_interrupt 的响应）
                        var ackMsg = JsonConvert.DeserializeObject<InterruptAckMessage>(json);
                        Debug.Log($"[WS] ✅ Interrupt acknowledged: {ackMsg.message}");
                        break;

                    case "scene.query_summary":
                    case "scene.query_objects":
                    case "scene.query_relations":
                    case "scene.query_surfaces":
                    case "scene.query_avatars":
                    case "scene.query_motions":
                    case "scene.query_sensors":
                    case "scene.query_avatar_candidates":
                    case "scene.validate_avatar_placement":
                    case "scene.capture_validation_views":
                    case "scene.find_sensor_placements":
                    case "scene.validate_placement":
                    case "scene.validate_actions":
                    case "scene.execute_actions":
                        OnSceneApiRequest?.Invoke(json);
                        break;

                    case "error":
                        var errorMsg = JsonConvert.DeserializeObject<ErrorMessage>(json);
                        if (errorMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize error payload: {json}");
                            break;
                        }

                        Debug.LogWarning(
                            string.IsNullOrWhiteSpace(errorMsg.details)
                                ? $"[WS] Server error: {errorMsg.message}"
                                : $"[WS] Server error: {errorMsg.message}\n{errorMsg.details}");
                        break;

                    case "job.started":
                    case "job.status":
                    case "job.completed":
                    case "job.cancelled":
                        var jobMsg = JsonConvert.DeserializeObject<JobLifecycleMessage>(json);
                        if (jobMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize job lifecycle payload: {json}");
                            break;
                        }

                        if (typeWrapper.type == "job.completed" || typeWrapper.type == "job.cancelled")
                        {
                            StopThinkingAnimation();
                        }
                        SafeInvoke(() => OnJobLifecycle?.Invoke(jobMsg), "job.OnJobLifecycle", json);
                        break;

                    case "task.result":
                        var taskMsg = JsonConvert.DeserializeObject<DelegatedTaskResultMessage>(json);
                        if (taskMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize task.result payload: {json}");
                            break;
                        }

                        StopThinkingAnimation();
                        SafeInvoke(() => OnDelegatedTaskResult?.Invoke(taskMsg), "task.result.OnDelegatedTaskResult", json);
                        break;

                    case "task.artifacts_snapshot":
                        var artifactSnapshotMsg = JsonConvert.DeserializeObject<DelegatedTaskArtifactsSnapshotMessage>(json);
                        if (artifactSnapshotMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize task.artifacts_snapshot payload: {json}");
                            break;
                        }

                        SafeInvoke(
                            () => OnDelegatedTaskArtifactsSnapshot?.Invoke(artifactSnapshotMsg),
                            "task.artifacts_snapshot.OnDelegatedTaskArtifactsSnapshot",
                            json);
                        break;

                    case "agent_push":
                        // Phase 3: HeartbeatHandler 在检测到显著场景事件时主动推送
                        var pushMsg = JsonConvert.DeserializeObject<AgentPushMessage>(json);
                        if (pushMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize agent_push payload: {json}");
                            break;
                        }

                        SafeInvoke(() => OnAgentPush?.Invoke(pushMsg), "agent_push.OnAgentPush", json);
                        // 控制指令走同一条控制管线（与 agent_ready 行为一致）
                        if (pushMsg.control != null && pushMsg.control.actions != null)
                        {
                            SafeInvoke(() => OnControl?.Invoke(pushMsg.control.actions), "agent_push.OnControl", json);
                        }

                        break;

                    case "server_config":
                        // Phase 3: 连接时由 Python 服务器立即发送，包含心跳间隔等配置
                        var cfgMsg = JsonConvert.DeserializeObject<ServerConfigMessage>(json);
                        if (cfgMsg == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize server_config payload: {json}");
                            break;
                        }

                        OnServerConfig?.Invoke(cfgMsg);
                        Debug.Log(
                            $"[WS] ⚙️ Server config received: heartbeat={cfgMsg.heartbeat_interval_s}s, tts_in_push={cfgMsg.tts_in_push}");
                        break;

                    case "client.hello_ack":
                        var helloAck = JsonConvert.DeserializeObject<ClientHelloAckMessage>(json);
                        if (helloAck == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize client.hello_ack payload: {json}");
                            break;
                        }

                        clientId = helloAck.client_id ?? string.Empty;
                        Debug.Log($"[WS] Client hello acknowledged: client_id={clientId}, username={helloAck.username}, us={helloAck.us}");
                        FlushPendingSceneActionLogs();
                        pendingOnConnected?.Invoke();
                        pendingOnConnected = null;
                        pendingOnFailed = null;
                        break;

                    case "sensor.recording_snapshot_ack":
                        var recordingAck = JsonConvert.DeserializeObject<SensorRecordingSnapshotAckMessage>(json);
                        if (recordingAck == null)
                        {
                            Debug.LogWarning($"[WS] Failed to deserialize sensor.recording_snapshot_ack payload: {json}");
                            break;
                        }

                        Debug.Log(
                            $"[WS] Recording snapshot synced: {recordingAck.timestamp_label} -> {recordingAck.saved_directory} ({recordingAck.saved_file_count} files)");
                        break;

                    default:
                        // 静默忽略未知消息类型，保持前向兼容性（不崩溃）
                        Debug.Log($"[WS] Ignoring unknown message type: '{typeWrapper.type}'");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WS] Failed to parse message: {ex.Message}\nPayload: {json}\n{ex}");
            }
        }

        private static AgentReplyMessage ToAgentReplyMessage(
            string type,
            string status,
            string transcription,
            string reply,
            string audioPath,
            ControlActions control)
        {
            return new AgentReplyMessage
            {
                type = type,
                status = string.IsNullOrEmpty(status) ? "success" : status,
                transcription = transcription,
                reply = reply,
                audio_path = audioPath,
                control = control ?? new ControlActions()
            };
        }

        public static void SendTranscribeRequest(string audioPath)
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                // 获取场景描述
                string sceneDescription = GetCurrentSceneDescription();

                var payload = new TranscribeRequest()
                {
                    type = "conversation.transcribe",
                    audio_path = audioPath,
                    scene_snapshot = sceneDescription,
                };

                string json = JsonConvert.SerializeObject(payload);
                websocket.SendText(json);

                StartThinkingAnimation();
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
                Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send reset request.");
            }
        }

        public static void SendTextChatRequest(string message)
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                // 获取场景描述
                string sceneDescription = GetCurrentSceneDescription();

                var payload = new ConversationAskRequest()
                {
                    message = message,
                    scene_snapshot = sceneDescription,
                    request_audio = false
                };

                string json = JsonConvert.SerializeObject(payload);
                websocket.SendText(json);

                StartThinkingAnimation();
            }
            else
            {
                Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send proposal selection.");
            }
        }

        public static void SendClarificationReply(string questionId, string[] selectedIds, string freeText)
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                var payload = new ClarificationReplyRequest()
                {
                    question_id = questionId,
                    selected_ids = selectedIds ?? Array.Empty<string>(),
                    free_text = freeText,
                    scene_snapshot = GetCurrentSceneDescription(),
                };

                websocket.SendText(JsonConvert.SerializeObject(payload));
                StartThinkingAnimation();
            }
            else
            {
                Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send clarification reply.");
            }
        }

        public static void SendProposalSelect(string proposalId, string[] selectedOptionIds, string note)
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                var payload = new ProposalSelectRequest()
                {
                    proposal_id = proposalId,
                    selected_option_ids = selectedOptionIds ?? Array.Empty<string>(),
                    note = note,
                    scene_snapshot = GetCurrentSceneDescription(),
                };

                websocket.SendText(JsonConvert.SerializeObject(payload));
                StartThinkingAnimation();
            }
            else
            {
                Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send text message.");
            }
        }

        public static void SendDelegatedTaskStart(
            string taskType,
            string goal,
            string selector,
            int recentN = 3,
            string timestampLabel = null)
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                var payload = new DelegatedTaskStartRequest()
                {
                    task_type = string.IsNullOrWhiteSpace(taskType) ? "analysis" : taskType,
                    goal = goal ?? string.Empty,
                    selector = string.IsNullOrWhiteSpace(selector) ? "latest" : selector,
                    recent_n = recentN <= 0 ? 3 : recentN,
                    timestamp_label = string.IsNullOrWhiteSpace(timestampLabel) ? null : timestampLabel,
                };

                websocket.SendText(JsonConvert.SerializeObject(payload));
                StartThinkingAnimation();
            }
            else
            {
                Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send delegated task request.");
            }
        }

        public static void SendAnalysisTaskLatest(string goal)
        {
            SendDelegatedTaskStart(
                taskType: "analysis",
                goal: goal,
                selector: "latest",
                recentN: 3);
        }

        public static void SendAnalysisTaskAll(string goal)
        {
            SendDelegatedTaskStart(
                taskType: "analysis",
                goal: goal,
                selector: "all",
                recentN: 3);
        }

        public static void SendAnalysisTaskRecent(int recentN = 3, string goal = null)
        {
            SendDelegatedTaskStart(
                taskType: "analysis",
                goal: goal,
                selector: "recent_n",
                recentN: recentN,
                timestampLabel: null);
        }

        public static void SendAnalysisTaskByLabel(string timestampLabel, string goal)
        {
            SendDelegatedTaskStart(
                taskType: "analysis",
                goal: goal,
                selector: "by_label",
                timestampLabel: timestampLabel);
        }

        /// <summary>
        /// 硬中断 — 取消正在进行的LLM调用，清除活动计划，Agent回到idle状态
        /// Python端返回: { "type": "interrupt_ack", "message": "Stopped." }
        /// </summary>
        public static void SendAgentInterrupt()
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                var payload = new AgentInterruptRequest();
                websocket.SendText(JsonConvert.SerializeObject(payload));
                Debug.Log("[WS] 🛑 Sent agent_interrupt (hard stop)");
            }
            else
            {
                Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send agent_interrupt.");
            }
        }

        /// <summary>
        /// 软中断 — 清除活动计划，但允许当前LLM调用完成
        /// Python端返回: { "type": "interrupt_ack", "message": "Stopped." }
        /// </summary>
        public static void SendPlanInterrupt()
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                var payload = new PlanInterruptRequest();
                websocket.SendText(JsonConvert.SerializeObject(payload));
                Debug.Log("[WS] ⏸️ Sent plan_interrupt (soft stop)");
            }
            else
            {
                Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send plan_interrupt.");
            }
        }

        public static bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;

        private static ObjectSelectionController EnsureObjectSelectionController()
        {
            if (ServiceLocator.IsRegistered<ObjectSelectionController>())
            {
                return ServiceLocator.Get<ObjectSelectionController>();
            }

            var existing = FindFirstObjectByType<ObjectSelectionController>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("ObjectSelectionController");
            return go.AddComponent<ObjectSelectionController>();
        }

        public static void SendMessage(object payload)
        {
            if (websocket == null || websocket.State != WebSocketState.Open)
            {
                Debug.LogWarning("[WS] ⚠️ WebSocket not connected, cannot send message.");
                return;
            }

            websocket.SendText(JsonConvert.SerializeObject(payload));
        }

        public static void SendObjectSelectionReply(ObjectSelectionReplyRequest payload)
        {
            SendMessage(payload);
        }

        public static void SendSceneActionLog(SceneActionRecord record, string sceneName = null)
        {
            if (record == null)
            {
                return;
            }

            var payload = new UnitySceneActionLogRequest
            {
                scene_name = string.IsNullOrWhiteSpace(sceneName) ? SceneManager.GetActiveScene().name : sceneName,
                record = record.ToNetworkLog()
            };

            try
            {
                if (HasOpenIdentifiedSession())
                {
                    websocket.SendText(JsonConvert.SerializeObject(payload));
                    return;
                }

                EnqueuePendingSceneActionLog(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WS] Failed to send unity.scene_action log: {ex.Message}\n{ex}");
            }
        }

        /// <summary>
        /// 发送场景心跳消息 (Phase 3)
        /// 由 HeartbeatManager 定时调用，Python 端用于 SceneDiff / EventClassifier。
        /// 仅在 WebSocket 已连接时发送；断线时静默跳过（不产生警告，避免日志污染）。
        /// </summary>
        public static void SendHeartbeat(string sceneSnapshot)
        {
            if (websocket != null && websocket.State == WebSocketState.Open)
            {
                var payload = new HeartbeatRequest { scene_snapshot = sceneSnapshot };
                websocket.SendText(JsonConvert.SerializeObject(payload));
            }
        }

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

        private static void StartThinkingAnimation()
        {
            var wsClient = ServiceLocator.Get<WsClient>();
            if (wsClient != null && wsClient.agentBehaviorController != null)
            {
                wsClient.agentBehaviorController.StartThinking();
            }
        }

        private static void StopThinkingAnimation()
        {
            var wsClient = ServiceLocator.Get<WsClient>();
            if (wsClient != null && wsClient.agentBehaviorController != null)
            {
                wsClient.agentBehaviorController.StopThinking();
            }
        }

        private static void SafeInvoke(Action action, string dispatchName, string payload)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WS] Failed during dispatch '{dispatchName}': {ex.Message}\nPayload: {payload}\n{ex}");
            }
        }

        private ClientHelloRequest BuildClientHelloRequest(string username, string us)
        {
            return new ClientHelloRequest
            {
                username = username ?? string.Empty,
                us = us ?? string.Empty,
            };
        }

        private void SendClientHello()
        {
            if (websocket == null || websocket.State != WebSocketState.Open)
            {
                return;
            }

            var payload = BuildClientHelloRequest(pendingUsername, pendingUs);
            websocket.SendText(JsonConvert.SerializeObject(payload));
        }

        private static bool HasOpenIdentifiedSession()
        {
            var wsClient = ServiceLocator.IsRegistered<WsClient>() ? ServiceLocator.Get<WsClient>() : null;
            return websocket != null &&
                   websocket.State == WebSocketState.Open &&
                   wsClient != null &&
                   !string.IsNullOrWhiteSpace(wsClient.clientId);
        }

        private static void EnqueuePendingSceneActionLog(UnitySceneActionLogRequest payload)
        {
            if (payload == null)
            {
                return;
            }

            while (PendingSceneActionLogs.Count >= MaxPendingSceneActionLogs)
            {
                PendingSceneActionLogs.Dequeue();
            }

            PendingSceneActionLogs.Enqueue(payload);
        }

        private static void FlushPendingSceneActionLogs()
        {
            if (!HasOpenIdentifiedSession())
            {
                return;
            }

            while (PendingSceneActionLogs.Count > 0)
            {
                websocket.SendText(JsonConvert.SerializeObject(PendingSceneActionLogs.Dequeue()));
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
