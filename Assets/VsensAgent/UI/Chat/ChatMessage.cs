using System;
using UnityEngine;

namespace VsensAgent.Data
{
    [Serializable]
    public class ChatMessage
{
    public enum MessageType 
    { 
        User,       // 用户发送的消息
        Agent,      // Agent回复的消息
        System      // 系统状态消息 (如"正在录音..."等)
    }

    [Header("消息内容")]
    public string content;              // 消息文本内容
    public MessageType type;            // 消息类型
    
    [Header("元数据")]
    public DateTime timestamp;          // 消息时间戳
    public string audioPath;            // Agent语音文件路径 (仅Agent消息)
    public bool isPlaying;              // 当前是否正在播放TTS (仅Agent消息)
    public string messageId;            // 唯一消息ID

    public ChatMessage()
    {
        timestamp = DateTime.Now;
        messageId = Guid.NewGuid().ToString();
        isPlaying = false;
    }

    public ChatMessage(string content, MessageType type) : this()
    {
        this.content = content;
        this.type = type;
    }

    public ChatMessage(string content, MessageType type, string audioPath) : this(content, type)
    {
        this.audioPath = audioPath;
    }

    /// <summary>
    /// 获取格式化的时间戳字符串
    /// </summary>
    public string GetFormattedTimestamp()
    {
        return timestamp.ToString("HH:mm");
    }

    /// <summary>
    /// 判断是否是Agent消息且有音频
    /// </summary>
    public bool HasAudio()
    {
        return type == MessageType.Agent && !string.IsNullOrEmpty(audioPath);
    }
}
}