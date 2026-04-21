using System;
using Newtonsoft.Json;

namespace VsensAgent.Network.Protocol
{
    /// <summary>
    /// 消息类型包装器 - 用于识别消息类型
    /// </summary>
    [Serializable]
    public class ClientHelloAckMessage
    {
        public string type;
        public string client_id;
        public string username;
        public string us;
        public string connected_at;
    }

    [Serializable]
    public class MessageTypeWrapper
    {
        public string type;
    }

    /// <summary>
    /// Agent回复消息 - 包含转录、回复内容和控制指令
    /// </summary>
    [Serializable]
    public class AgentReplyMessage
    {
        public string type;
        public string status;
        public string transcription;
        public string reply;
        public string audio_path;
        
        [JsonProperty("control")]
        public ControlActions control;
    }

    [Serializable]
    public class ConversationReplyMessage
    {
        public string type;
        public string status;
        public string turn_id;
        public string transcription;
        public string reply;
        public string audio_path;
        public string legacy_type;

        [JsonProperty("control")]
        public ControlActions control;
    }

    /// <summary>
    /// 控制动作包装器 - 包含一组控制指令
    /// </summary>
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
    public class ClarificationOption
    {
        public string id;
        public string label;
        public string description;
    }

    [Serializable]
    public class ClarificationRequestMessage
    {
        public string type;
        public string status;
        public string question_id;
        public string prompt;
        public string selection_mode;
        public string transcription;
        public string reply;
        public string audio_path;
        public ClarificationOption[] options;

        [JsonProperty("control")]
        public ControlActions control;
    }

    [Serializable]
    public class ProposalOption
    {
        public string id;
        public string label;
        public string description;
    }

    [Serializable]
    public class ProposalReadyMessage
    {
        public string type;
        public string status;
        public string proposal_id;
        public string title;
        public string summary;
        public string transcription;
        public string reply;
        public string audio_path;
        public ProposalOption[] options;

        [JsonProperty("control")]
        public ControlActions control;
    }

    [Serializable]
    public class JobLifecycleMessage
    {
        public string type;
        public string job_id;
        public string job_kind;
        public string status;
        public string reason;
    }

    [Serializable]
    public class DataAnalysisResultMessage
    {
        public string type;
        public string status;
        public string job_id;
        public string job_kind;
        public string reply;
        public string summary;
        public string har_judgement;
        public int recording_count;
        public string[] recording_keys;
        public string[] aggregate_findings;
        public string scene_context_summary;
    }

    /// <summary>
    /// Agent状态广播消息 - Python端主动推送的Agent运行状态
    /// 状态值: idle | listening | transcribing | thinking | planning |
    ///         executing | speaking | waiting | scripting
    /// </summary>
    [Serializable]
    public class AgentStatusMessage
    {
        public string type;
        public string state;
        public string detail;
    }

    /// <summary>
    /// 中断确认消息 - 响应 agent_interrupt 或 plan_interrupt 请求的回复
    /// </summary>
    [Serializable]
    public class InterruptAckMessage
    {
        public string type;
        public string message;
    }

    /// <summary>
    /// Agent 主动推送消息 (Phase 3) - 由 Python HeartbeatHandler 在检测到显著场景事件时主动发送
    /// （无需 Unity 请求）。可包含语音回复和/或控制指令。
    /// Python 端 severity >= 0.5 才触发此消息，所以 Unity 端不会收到噪声推送。
    /// 
    /// triggered_by: 触发此推送的事件列表（如 "SENSOR_TRIGGER:OPTICAL-01"），可用于 Debug / HUD 显示
    /// </summary>
    [Serializable]
    public class AgentPushMessage
    {
        public string type;

        /// <summary>Primary event type that triggered this push (e.g. SENSOR_TRIGGER).</summary>
        public string trigger;

        /// <summary>Agent reply text. Never empty — Python suppresses the push if LLM returned nothing.</summary>
        public string reply;

        /// <summary>
        /// Absolute path to a pre-generated MP3 file (Python TTS).
        /// Present only when server_config.tts_in_push == true.
        /// Always null-check before use.
        /// </summary>
        [JsonProperty("audio_path")]
        public string audio_path;

        [JsonProperty("control")]
        public ControlActions control;

        /// <summary>
        /// Human-readable list of events that crossed the attention threshold,
        /// e.g. ["SENSOR_TRIGGER:OPTICAL-01 (severity 0.8)"]. For debug / HUD display only.
        /// </summary>
        [JsonProperty("triggered_by")]
        public string[] triggered_by;
    }

    /// <summary>
    /// 服务器配置消息 (Phase 3) - 客户端连接成功后由 Python 服务器立即推送。
    /// Unity 应进行以下操作：
    ///   - 使用 heartbeat_interval_s 覆盖 HeartbeatManager 的默认间隔
    ///   - 了解 tts_in_push (是否显示音频播放按鈕)
    ///   - 了解 attention_threshold (供 HUD 显示，无需 Unity 处理)
    /// </summary>
    [Serializable]
    public class ServerConfigMessage
    {
        public string type;

        [JsonProperty("heartbeat_interval_s")]
        public float heartbeat_interval_s;

        [JsonProperty("tts_in_push")]
        public bool tts_in_push;

        [JsonProperty("attention_threshold")]
        public float attention_threshold;
    }

    [Serializable]
    public class ErrorMessage
    {
        public string type;
        public string message;
        public string details;
        public string code;
        public string request_id;
    }

    [Serializable]
    public class SensorRecordingSnapshotAckMessage
    {
        public string type;
        public string timestamp_label;
        public string saved_directory;
        public string[] saved_files;
        public int saved_file_count;
    }
}
