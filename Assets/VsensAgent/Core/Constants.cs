using UnityEngine;

namespace VsensAgent.Core
{
    /// <summary>
    /// 集中管理所有常量配置，避免魔法数字和硬编码字符串
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// 网络配置常量
        /// </summary>
        public static class Network
        {
            /// <summary>
            /// WebSocket 服务器地址
            /// </summary>
            public const string WS_SERVER_URL = "localhost:8765";
            // public const string WS_SERVER_URL = "131.113.87.66:9333";

            
            /// <summary>
            /// WebSocket 重连间隔（秒）
            /// </summary>
            public const float RECONNECT_INTERVAL = 3f;
        }

        /// <summary>
        /// 音频配置常量
        /// </summary>
        public static class Audio
        {
            /// <summary>
            /// 默认采样率（16kHz，适合语音识别）
            /// </summary>
            public const int DEFAULT_SAMPLE_RATE = 16000;
            
            /// <summary>
            /// 最大录音时长（秒）
            /// </summary>
            public const int MAX_RECORDING_DURATION = 60;
        }

        /// <summary>
        /// 输入按键配置
        /// </summary>
        public static class InputKeys
        {
            /// <summary>
            /// 语音录制按键（按住录制，松开发送）
            /// </summary>
            public static readonly KeyCode VOICE_RECORD = KeyCode.R;
            
            /// <summary>
            /// 切换相机视角按键
            /// </summary>
            public static readonly KeyCode TOGGLE_VIEW = KeyCode.Tab;
            
            /// <summary>
            /// 取消UI焦点按键
            /// </summary>
            public static readonly KeyCode UNFOCUS = KeyCode.Escape;
        }

        /// <summary>
        /// UI配置常量
        /// </summary>
        public static class UI
        {
            /// <summary>
            /// 聊天消息最大历史记录数
            /// </summary>
            public const int MAX_CHAT_HISTORY = 100;
            
            /// <summary>
            /// 传感器数据更新间隔（秒）
            /// </summary>
            public const float SENSOR_UPDATE_INTERVAL = 0.5f;
            
            /// <summary>
            /// 传感器列表刷新间隔（秒）
            /// </summary>
            public const float SENSOR_LIST_REFRESH_INTERVAL = 2f;
        }

        /// <summary>
        /// 相机控制配置
        /// </summary>
        public static class Camera
        {
            /// <summary>
            /// 第一人称移动速度
            /// </summary>
            public const float FIRST_PERSON_MOVEMENT_SPEED = 5f;
            
            /// <summary>
            /// 第一人称视角速度
            /// </summary>
            public const float FIRST_PERSON_LOOK_SPEED = 2f;
            
            /// <summary>
            /// 上帝视角移动速度
            /// </summary>
            public const float GOD_VIEW_MOVEMENT_SPEED = 10f;
            
            /// <summary>
            /// 上帝视角旋转速度
            /// </summary>
            public const float GOD_VIEW_LOOK_SPEED = 2f;
            
            /// <summary>
            /// 上帝视角默认距离
            /// </summary>
            public const float GOD_VIEW_DISTANCE = 15f;
            
            /// <summary>
            /// 上帝视角俯视角度
            /// </summary>
            public const float GOD_VIEW_ANGLE = 45f;
            
            /// <summary>
            /// 上帝视角滚轮缩放速度
            /// </summary>
            public const float GOD_VIEW_SCROLL_SPEED = 2f;
            
            /// <summary>
            /// 上帝视角最小距离
            /// </summary>
            public const float GOD_VIEW_MIN_DISTANCE = 5f;
            
            /// <summary>
            /// 上帝视角最大距离
            /// </summary>
            public const float GOD_VIEW_MAX_DISTANCE = 40f;
            
            /// <summary>
            /// 视角切换过渡速度
            /// </summary>
            public const float TRANSITION_SPEED = 5f;
        }

        /// <summary>
        /// 心跳配置常量 (Phase 3)
        /// </summary>
        public static class Heartbeat
        {
            /// <summary>
            /// Python 端 SceneDiff / EventClassifier 所需的场景心跳发送间隔（秒）
            /// 建议范围: 0.5s – 2.0s。默认 1.0s。
            /// </summary>
            public const float HEARTBEAT_INTERVAL = 1.0f;
        }
    }
}
