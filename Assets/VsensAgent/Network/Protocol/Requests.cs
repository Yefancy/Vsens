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
    /// 重置请求 - 清空对话历史
    /// </summary>
    [Serializable]
    public class ResetRequest
    {
        public string type;
    }
}
