using System.Collections;
using UnityEngine;
using VsensAgent.Core;

namespace VsensAgent.Network
{
    /// <summary>
    /// Phase 3 — HeartbeatManager
    ///
    /// 以固定间隔向 Python 后端发送 scene_heartbeat 消息。
    /// Python 端的 HeartbeatHandler 使用 SceneDiff + EventClassifier 检测
    /// 场景变化，仅在显著事件（severity >= 0.5）时回复 agent_push 消息。
    ///
    /// 使用方法: 将此组件挂载到与 WsClient 同层级的 GameObject 上（例如
    ///           VsensAgentManager 或专用的 NetworkManager 对象）。
    ///           启用 enableHeartbeat 即可开始心跳。
    /// </summary>
    public class HeartbeatManager : MonoBehaviour
    {
        /// <summary>
        /// 心跳发送间隔（秒）。默认使用 Constants.Heartbeat.HEARTBEAT_INTERVAL（1.0s）。
        /// Python 端 SceneDiff 在 0.5s–2.0s 范围内均可正常检测。
        /// </summary>
        [SerializeField]
        [Tooltip("场景心跳发送间隔（秒）。建议范围: 0.5s – 2.0s。")]
        private float heartbeatInterval = Constants.Heartbeat.HEARTBEAT_INTERVAL;

        /// <summary>
        /// 是否启用心跳。可在 Inspector 中实时关闭以调试无心跳状态。
        /// </summary>
        [SerializeField]
        [Tooltip("取消勾选可在保持 WsClient 连接的同时停止发送心跳（调试用）。")]
        private bool enableHeartbeat = false;

        /// <summary>
        /// 当 Agent 正忙（非 idle）时是否暂停心跳。
        /// 启用后可减少 Python 端在 LLM 调用期间的无用 diff 计算，
        /// 但同时会导致 Agent 忙时不会触发新的 agent_push。
        /// 默认 true：Agent 忙时暂停心跳，减少 Python 端在 LLM 调用期间的无用 diff 计算。
        /// </summary>
        [SerializeField]
        [Tooltip("Agent 忙时暂停心跳可节省带宽，但会错过该窗口内的场景事件。")]
        private bool pauseWhenAgentBusy = true;

        // 当前 Agent 是否处于 idle 状态（由 agent_status 消息驱动）
        private bool _agentIsIdle = true;
        private Coroutine _heartbeatCoroutine;

        // ------------------------------------------------------------------ //
        //  Unity lifecycle
        // ------------------------------------------------------------------ //

        void OnEnable()
        {
            WsClient.OnAgentStatus  += HandleAgentStatus;
            WsClient.OnServerConfig += HandleServerConfig;

            if (enableHeartbeat)
                _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
        }

        void OnDisable()
        {
            WsClient.OnAgentStatus  -= HandleAgentStatus;
            WsClient.OnServerConfig -= HandleServerConfig;

            if (_heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
            }
        }

        // ------------------------------------------------------------------ //
        //  Heartbeat loop
        // ------------------------------------------------------------------ //

        private IEnumerator HeartbeatLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(heartbeatInterval);

                // 跳过：WS 未连接
                if (!WsClient.IsConnected) continue;

                // 跳过：Agent 正忙（可选）
                if (pauseWhenAgentBusy && !_agentIsIdle) continue;

                string snapshot = WsClient.GetCurrentSceneDescription();
                WsClient.SendHeartbeat(snapshot);
            }
        }

        // ------------------------------------------------------------------ //
        //  Agent status tracking (for pauseWhenAgentBusy)
        // ------------------------------------------------------------------ //

        private void HandleAgentStatus(Protocol.AgentStatusMessage msg)
        {
            if (msg == null) return;
            _agentIsIdle = (msg.state == "idle");
        }

        /// <summary>
        /// Python 服务器在每次连接时推送 server_config。
        /// 如果服务器下发了 heartbeat_interval_s，使用它覆盖本地 Inspector 设置，
        /// 以确保 Unity 与 Python SceneDiff 的轮询率一致。
        /// </summary>
        private void HandleServerConfig(Protocol.ServerConfigMessage cfg)
        {
            if (cfg == null) return;

            if (cfg.heartbeat_interval_s > 0f && !Mathf.Approximately(cfg.heartbeat_interval_s, heartbeatInterval))
            {
                Debug.Log($"[HeartbeatManager] Server config: updating heartbeat interval {heartbeatInterval}s → {cfg.heartbeat_interval_s}s");
                heartbeatInterval = cfg.heartbeat_interval_s;

                // Restart the loop so the new interval takes effect immediately
                if (_heartbeatCoroutine != null)
                {
                    StopCoroutine(_heartbeatCoroutine);
                    _heartbeatCoroutine = null;
                }
                if (enableHeartbeat && isActiveAndEnabled)
                    _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API — Inspector / runtime configuration
        // ------------------------------------------------------------------ //

        /// <summary>当前是否启用了 heartbeat（用户期望状态）。</summary>
        public bool IsHeartbeatEnabled => enableHeartbeat;

        /// <summary>当前 heartbeat 协程是否在运行。</summary>
        public bool IsHeartbeatLoopRunning => _heartbeatCoroutine != null;

        /// <summary>启用或禁用心跳（运行时可调用）。</summary>
        public void SetHeartbeatEnabled(bool enabled)
        {
            enableHeartbeat = enabled;

            if (enabled && _heartbeatCoroutine == null && isActiveAndEnabled)
            {
                _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());
            }
            else if (!enabled && _heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
            }
        }

        /// <summary>立即发送一次心跳（调试/测试用）。</summary>
        public void SendHeartbeatNow()
        {
            if (!WsClient.IsConnected)
            {
                Debug.LogWarning("[HeartbeatManager] Cannot send heartbeat: WebSocket not connected.");
                return;
            }
            WsClient.SendHeartbeat(WsClient.GetCurrentSceneDescription());
        }
    }
}
