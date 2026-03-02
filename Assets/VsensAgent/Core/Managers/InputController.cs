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
            
            // 从服务定位器查找语音录音器（可能为null，稍后会通过UpdateAudioRecorder更新）
            audioRecorder = ServiceLocator.Get<AudioRecorder>();
            if (audioRecorder == null)
            {
                Debug.LogWarning("[InputController] ⚠️ AudioRecorder not found during Initialize, will retry later");
            }
        }
        
        /// <summary>
        /// 更新AudioRecorder引用（由ChatUIManager在OnEnable时调用）
        /// </summary>
        public void UpdateAudioRecorder(AudioRecorder recorder)
        {
            audioRecorder = recorder;
            if (audioRecorder != null)
            {
                Debug.Log("[InputController] ✅ AudioRecorder reference updated successfully");
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
            
            // Lazy-resolve: AudioRecorder may not have been registered yet when
            // Initialize()/UpdateAudioRecorder() were called (Unity Awake/OnEnable
            // order across GameObjects is not guaranteed).
            if (audioRecorder == null)
            {
                audioRecorder = ServiceLocator.Get<AudioRecorder>();
                if (audioRecorder != null)
                    Debug.Log("[InputController] ✅ AudioRecorder resolved via lazy lookup.");
            }
            
            if (audioRecorder != null)
            {
                audioRecorder.SetInputEnabled(enabled);
                Debug.Log($"[InputController] 🎙️ Voice recording {(enabled ? "ENABLED" : "DISABLED")}");
            }
            else
            {
                Debug.LogWarning("[InputController] ⚠️ AudioRecorder still null during lazy lookup — R key block will not apply.");
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