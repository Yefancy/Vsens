using System;
using Newtonsoft.Json;

namespace VsensAgent.Network.Protocol
{
    /// <summary>
    /// 消息类型包装器 - 用于识别消息类型
    /// </summary>
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
}
