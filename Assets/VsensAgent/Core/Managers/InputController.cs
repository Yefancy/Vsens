using UnityEngine;
using VsensAgent.Audio;
using VsensAgent.Core;

namespace VsensAgent
{
    /// <summary>
    /// 输入管理器 - 管理相机控制和语音录音的启用/禁用
    /// 不需要作为MonoBehaviour挂在场景中，由ChatUIManager内部使用
    /// </summary>
    public class InputController
    {
        private bool enableCameraControl = true;
        private bool enableVoiceRecording = true;
        private MonoBehaviour[] cameraControllers;
        private AudioRecorder audioRecorder;
        
        public void Initialize()
        {
            // 查找相机控制器
            AutoFindCameraControllers();
            
            // 从服务定位器查找语音录音器
            audioRecorder = ServiceLocator.Get<AudioRecorder>();
            if (audioRecorder == null)
            {
                Debug.LogWarning("[InputManager] ⚠️ AudioRecorder not found");
            }
        }
        
        /// <summary>
        /// 当聊天输入框焦点状态改变时调用
        /// </summary>
        public void OnChatFocusChanged(bool isFocused)
        {
            SetCameraControlEnabled(!isFocused);
            SetVoiceRecordingEnabled(!isFocused);
        }
        
        private void SetCameraControlEnabled(bool enabled)
        {
            enableCameraControl = enabled;
            
            if (cameraControllers != null)
            {
                foreach (var controller in cameraControllers)
                {
                    if (controller != null)
                    {
                        var userCameraControl = controller as UserMainCameraControl;
                        if (userCameraControl != null)
                        {
                            userCameraControl.SetInputEnabled(enabled);
                        }
                        else
                        {
                            controller.enabled = enabled;
                        }
                    }
                }
            }
        }
        
        private void SetVoiceRecordingEnabled(bool enabled)
        {
            enableVoiceRecording = enabled;
            
            if (audioRecorder != null)
            {
                audioRecorder.SetInputEnabled(enabled);
            }
            else
            {
                Debug.LogWarning("[InputManager] ⚠️ AudioRecorder is null, cannot control voice recording!");
            }
        }
        
        private void AutoFindCameraControllers()
        {
            var controllers = new System.Collections.Generic.List<MonoBehaviour>();
            
            UserMainCameraControl userCameraControl = ServiceLocator.Get<UserMainCameraControl>();
            if (userCameraControl != null)
            {
                controllers.Add(userCameraControl);
                Debug.Log($"[InputManager] 🎯 Found UserMainCameraControl: {userCameraControl.name}");
            }
            
            cameraControllers = controllers.ToArray();
            Debug.Log($"[InputManager] 📋 Total camera controllers found: {controllers.Count}");
        }
    }
}