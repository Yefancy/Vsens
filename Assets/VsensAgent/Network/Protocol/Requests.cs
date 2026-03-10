using System;

namespace VsensAgent.Network.Protocol
{
    /// <summary>
    /// 转录请求 - 发送音频文件进行转录和回复
    /// </summary>
    [Serializable]
    public class TranscribeRequest
    {
        public string type;
        public string audio_path;
        public string scene_snapshot;
    }

    /// <summary>
    /// 文本聊天请求 - 发送文本消息获取回复
    /// </summary>
    [Serializable]
    public class TextChatRequest
    {
        public string type;
        public string message;
        public string scene_snapshot;
        public bool request_audio;
    }

    /// <summary>
    /// 协作协议文本请求 - 语义上等同于 text_chat，用于逐步迁移到新的消息族。
    /// </summary>
    [Serializable]
    public class ConversationAskRequest
    {
        public string type = "conversation.ask";
        public string message;
        public string scene_snapshot;
        public bool request_audio;
    }
    
    /// <summary>
    /// 重置请求 - 清空对话历史（不清除LanceDB情节记忆）
    /// </summary>
    [Serializable]
    public class ResetRequest
    {
        public string type;
    }

    /// <summary>
    /// 硬中断请求 - 取消正在进行的LLM调用，清除活动计划，Agent回到idle状态
    /// Python端返回: { "type": "interrupt_ack", "message": "Stopped." }
    /// </summary>
    [Serializable]
    public class AgentInterruptRequest
    {
        public string type = "agent_interrupt";
    }

    /// <summary>
    /// 软中断请求 - 清除活动计划，但允许当前LLM调用完成
    /// Python端返回: { "type": "interrupt_ack", "message": "Stopped." }
    /// </summary>
    [Serializable]
    public class PlanInterruptRequest
    {
        public string type = "plan_interrupt";
    }

    /// <summary>
    /// 场景心跳请求 (Phase 3) - 定期发送场景快照供 Python 端 SceneDiff / EventClassifier 使用
    /// 发送间隔由 HeartbeatManager 控制（默认 1 秒）。
    /// Python 端仅在检测到高显著性事件时返回 agent_push 消息；无事件时静默。
    /// </summary>
    [Serializable]
    public class HeartbeatRequest
    {
        public string type = "scene_heartbeat";
        public string scene_snapshot;
    }

    /// <summary>
    /// 对 clarification.request 的响应。当前仅作协议占位，后续由 UI 驱动发送。
    /// </summary>
    [Serializable]
    public class ClarificationReplyRequest
    {
        public string type = "clarification.reply";
        public string question_id;
        public string[] selected_ids;
        public string free_text;
        public string scene_snapshot;
    }

    /// <summary>
    /// 对 proposal.ready 的响应。当前仅作协议占位，后续由 UI 驱动发送。
    /// </summary>
    [Serializable]
    public class ProposalSelectRequest
    {
        public string type = "proposal.select";
        public string proposal_id;
        public string[] selected_option_ids;
        public string note;
        public string scene_snapshot;
    }
}
