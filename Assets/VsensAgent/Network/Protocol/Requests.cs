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
}
