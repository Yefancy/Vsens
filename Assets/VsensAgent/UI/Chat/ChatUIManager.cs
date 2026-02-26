using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using VsensAgent.Data;
using VsensAgent.Audio;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;
using VsensAgent.Core;

namespace VsensAgent.UI
{
    public class ChatUIManager : MonoBehaviour
    {
        [Header("UI组件引用")]
        public GameObject chatWindow;                   // 整个聊天窗口
        public ScrollRect messageScrollRect;            // 消息滚动区域
        public Transform messageContainer;              // 消息容器 (ScrollRect的Content)
        public TMP_InputField textInputField;          // 文字输入框
        public Button sendButton;                      // 发送按钮
        public Button toggleViewButton;                 // 切换视角按钮
        public GameObject voiceInputIndicator;          // 语音录制指示器

        [Header("消息预制件")]
        public GameObject userMessagePrefab;           // 用户消息预制件
        public GameObject agentMessagePrefab;          // Agent消息预制件
        public GameObject systemMessagePrefab;         // 系统消息预制件

        [Header("设置")]
        public int maxMessageHistory = Constants.UI.MAX_CHAT_HISTORY;            // 最大消息历史数量
        public float autoScrollSpeed = 1f;             // 自动滚动速度
        public bool autoFocusInput = true;             // 是否自动聚焦输入框

        [Header("交互控制")]
        public bool disableCameraWhenFocused = true;    // 聚焦时禁用相机控制
        public KeyCode unfocusKey = Constants.InputKeys.UNFOCUS;     // 取消聚焦的快捷键

        // 私有变量
        private List<ChatMessage> messageHistory = new List<ChatMessage>();
        private Dictionary<string, GameObject> messageUIObjects = new Dictionary<string, GameObject>();
        private Coroutine autoScrollCoroutine;
        private AudioRecorder audioRecorder;
        private bool isRecording = false;
        private bool wasInputFocused = false;  // 跟踪输入框聚焦状态
        private Canvas MainCanvas;              // 用于点击检测
        private InputController inputController;      // 输入管理器

        // 事件定义
        public static System.Action<string> OnUserMessageSent;          // 用户发送消息事件
        public static System.Action<ChatMessage> OnMessageAdded;        // 消息添加事件
        public static System.Action<bool> OnInputFocusChanged;          // 输入框聚焦状态变化事件

        void Awake()
        {
            // 确保组件引用
            if (messageScrollRect == null)
                messageScrollRect = GetComponentInChildren<ScrollRect>();
            if (textInputField == null)
                textInputField = GetComponentInChildren<TMP_InputField>();
            if (sendButton == null)
                sendButton = GetComponentInChildren<Button>();

            // 初始化
            InitializeUI();
            
            // 获取Canvas引用用于点击检测
            MainCanvas = GetComponentInParent<Canvas>();
            
            // 初始化输入管理器
            inputController = new InputController();
            inputController.Initialize();
            
            // 注册到服务定位器
            ServiceLocator.Register<ChatUIManager>(this);
        }

        void OnEnable()
        {
            // 订阅WebSocket事件
            WsClient.OnAgentReply += OnAgentReplyReceived;
            
            // 从服务定位器获取AudioRecorder引用
            audioRecorder = ServiceLocator.Get<AudioRecorder>();
            if (audioRecorder != null)
            {
                Debug.Log("[ChatUIManager] ✅ AudioRecorder service found");
            }
            else
            {
                Debug.LogWarning("[ChatUIManager] ⚠️ AudioRecorder not found, voice input disabled");
            }

            // 订阅输入事件
            if (sendButton != null)
                sendButton.onClick.AddListener(SendTextMessage);
            if (textInputField != null)
                textInputField.onSubmit.AddListener(OnTextInputSubmit);
            if (toggleViewButton != null)
                toggleViewButton.onClick.AddListener(OnToggleViewClicked);
        }

        void OnDisable()
        {
            // 取消订阅
            WsClient.OnAgentReply -= OnAgentReplyReceived;
            
            if (sendButton != null)
                sendButton.onClick.RemoveListener(SendTextMessage);
            if (textInputField != null)
                textInputField.onSubmit.RemoveListener(OnTextInputSubmit);
            if (toggleViewButton != null)
                toggleViewButton.onClick.RemoveListener(OnToggleViewClicked);
        }

        void Update()
        {
            // 检测输入框聚焦变化
            CheckInputFocusChange();
            
            // 处理取消聚焦的输入
            HandleUnfocusInput();
            
            // 修改后的自动聚焦逻辑
            HandleAutoFocus();
        }

        private void InitializeUI()
        {
            // 初始化UI状态
            if (chatWindow != null)
                chatWindow.SetActive(true);
            
            if (voiceInputIndicator != null)
                voiceInputIndicator.SetActive(false);

            if (textInputField != null)
            {
                textInputField.text = "";
                textInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "Type your message here... Or R to record voice.";
            }

            if (messageContainer != null)
            {
                var rectTransform = messageContainer.GetComponent<RectTransform>();
                var contentSizeFitter = messageContainer.GetComponent<ContentSizeFitter>();
                var layoutGroup = messageContainer.GetComponent<VerticalLayoutGroup>();
                
                if (contentSizeFitter != null)
                {
                    if (contentSizeFitter.verticalFit != ContentSizeFitter.FitMode.PreferredSize)
                    {
                        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    }
                }
                
                if (layoutGroup != null)
                {
                    layoutGroup.childControlHeight = true;
                    layoutGroup.childControlWidth = true;
                    layoutGroup.childForceExpandHeight = false;
                    layoutGroup.childForceExpandWidth = true;
                }
                
                Canvas.ForceUpdateCanvases();
                
                if (rectTransform.rect.width <= 0 || rectTransform.rect.height <= 0)
                {
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(1, 1);
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                    Canvas.ForceUpdateCanvases();
                }
            }
            
            // 添加欢迎消息
            AddSystemMessage("VsensAgent ready, say hi! Or press R to record voice.");
        }

        // ========== 消息处理方法 ==========

        /// <summary>
        /// 添加消息到聊天历史
        /// </summary>
        public void AddMessage(ChatMessage message)
        {
            if (message == null) return;

            // 添加到历史记录
            messageHistory.Add(message);

            // 清理旧消息 (保持最大数量限制)
            if (messageHistory.Count > maxMessageHistory)
            {
                var oldMessage = messageHistory[0];
                messageHistory.RemoveAt(0);
                
                // 移除对应的UI对象
                if (messageUIObjects.ContainsKey(oldMessage.messageId))
                {
                    if (messageUIObjects[oldMessage.messageId] != null)
                        Destroy(messageUIObjects[oldMessage.messageId]);
                    messageUIObjects.Remove(oldMessage.messageId);
                }
            }

            // 创建UI显示
            CreateMessageUI(message);

            // 自动滚动到底部
            StartAutoScroll();

            // 触发事件
            OnMessageAdded?.Invoke(message);
        }

        public void AddUserMessage(string content)
        {
            var message = new ChatMessage(content, ChatMessage.MessageType.User);
            AddMessage(message);
            OnUserMessageSent?.Invoke(content);
        }

        public void AddAgentMessage(string content, string audioPath = "")
        {
            var message = new ChatMessage(content, ChatMessage.MessageType.Agent, audioPath);
            AddMessage(message);
        }

        public void AddSystemMessage(string content)
        {
            var message = new ChatMessage(content, ChatMessage.MessageType.System);
            AddMessage(message);
        }

        // ========== UI创建方法 ==========

        private void CreateMessageUI(ChatMessage message)
        {
            GameObject prefab = null;
            switch (message.type)
            {
                case ChatMessage.MessageType.User:
                    prefab = userMessagePrefab;
                    break;
                case ChatMessage.MessageType.Agent:
                    prefab = agentMessagePrefab;
                    break;
                case ChatMessage.MessageType.System:
                    prefab = systemMessagePrefab;
                    break;
            }

            if (prefab == null)
            {
                Debug.LogError($"[ChatUIManager] ❌ No prefab found for message type: {message.type}");
                return;
            }
            
            if (messageContainer == null)
            {
                Debug.LogError($"[ChatUIManager] ❌ MessageContainer is NULL!");
                return;
            }

            // 实例化消息UI
            GameObject messageUI = Instantiate(prefab, messageContainer);
            
            messageUIObjects[message.messageId] = messageUI;

            // 设置消息内容
            SetupMessageUI(messageUI, message);
        }

        private void SetupMessageUI(GameObject messageUI, ChatMessage message)
        {
            // 查找文本组件并设置内容
            TextMeshProUGUI contentText = messageUI.GetComponentInChildren<TextMeshProUGUI>();
            
            if (contentText != null)
            {
                contentText.text = message.content;
            }
            else
            {
                Debug.LogError($"[ChatUIManager] ❌ No TextMeshProUGUI found in {messageUI.name}!");
            }

            // 如果是Agent消息且有音频，设置播放按钮
            if (message.HasAudio())
            {
                Button playButton = messageUI.transform.Find("PlayButton")?.GetComponent<Button>();
                if (playButton != null)
                {
                    playButton.onClick.AddListener(() => PlayAgentAudio(message));
                }
            }
        }

        // ========== 输入处理方法 ==========

        private void SendTextMessage()
        {
            if (textInputField == null || string.IsNullOrWhiteSpace(textInputField.text)) 
                return;

            string messageContent = textInputField.text.Trim();
            
            // 添加用户消息到聊天界面
            AddUserMessage(messageContent);

            // 发送到WebSocket (这里需要根据实际的WebSocket接口来调整)
            SendMessageToAgent(messageContent);

            // 清空输入框
            textInputField.text = "";
            textInputField.Select();
        }

        private void OnTextInputSubmit(string text)
        {
            SendTextMessage();
            // 发送后取消聚焦，让相机控制恢复
            UnfocusInputField();
        }

        // ========== 焦点管理方法 ==========

        /// <summary>
        /// 检测输入框聚焦状态变化并通知其他组件
        /// </summary>
        private void CheckInputFocusChange()
        {
            if (textInputField == null) return;
            
            bool isCurrentlyFocused = textInputField.isFocused;
            
            if (isCurrentlyFocused != wasInputFocused)
            {
                wasInputFocused = isCurrentlyFocused;
                
                // 通知输入管理器聊天焦点状态变化
                inputController?.OnChatFocusChanged(isCurrentlyFocused);
                
                // 通知其他组件聚焦状态变化
                OnInputFocusChanged?.Invoke(isCurrentlyFocused);
            }
        }

        /// <summary>
        /// 处理取消聚焦的输入（ESC键和点击空白区域）
        /// </summary>
        private void HandleUnfocusInput()
        {
            // ESC键取消聚焦
            if (Input.GetKeyDown(unfocusKey) && textInputField != null && textInputField.isFocused)
            {
                UnfocusInputField();
                return;
            }
            
            // 检测鼠标左键点击空白区域
            if (Input.GetMouseButtonDown(0))
            {
                CheckClickOutsideInput();
            }
        }

        /// <summary>
        /// 检测是否点击了UI外部区域
        /// </summary>
        private void CheckClickOutsideInput()
        {
            if (textInputField == null || !textInputField.isFocused) return;
            
            // 将鼠标位置转换为Canvas坐标
            Vector2 mousePos = Input.mousePosition;
            
            // 检测是否点击在聊天窗口内
            if (MainCanvas != null && chatWindow != null && RectTransformUtility.RectangleContainsScreenPoint(
                chatWindow.GetComponent<RectTransform>(), 
                mousePos, 
                MainCanvas.worldCamera))
            {
                // 点击在聊天窗口内，检查是否点击在输入框上
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                    textInputField.GetComponent<RectTransform>(), 
                    mousePos, 
                    MainCanvas.worldCamera))
                {
                    // 点击在聊天窗口内但不在输入框上，取消聚焦
                    UnfocusInputField();
                }
            }
            else
            {
                // 点击在聊天窗口外，取消聚焦
                UnfocusInputField();
            }
        }

        /// <summary>
        /// 取消输入框聚焦
        /// </summary>
        private void UnfocusInputField()
        {
            if (textInputField != null)
            {
                textInputField.DeactivateInputField();
                // 确保没有UI元素被选中
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// 修改后的自动聚焦逻辑
        /// </summary>
        private void HandleAutoFocus()
        {
            if (!autoFocusInput || textInputField == null || isRecording) return;
            
            // 只在没有按下WASD键时才自动聚焦
            bool isMovementKeyPressed = Input.GetKey(KeyCode.W) || 
                                       Input.GetKey(KeyCode.A) || 
                                       Input.GetKey(KeyCode.S) || 
                                       Input.GetKey(KeyCode.D);
            
            if (!textInputField.isFocused && !isMovementKeyPressed)
            {
                textInputField.Select();
            }
        }

        /// <summary>
        /// 检查当前输入框是否聚焦（供外部调用）
        /// </summary>
        public bool IsInputFocused()
        {
            return textInputField != null && textInputField.isFocused;
        }

        /// <summary>
        /// 手动聚焦输入框
        /// </summary>
        public void FocusInputField()
        {
            if (textInputField != null)
            {
                textInputField.Select();
                textInputField.ActivateInputField();
            }
        }



        /// <summary>
        /// 显示语音录制状态 (由AudioRecorder调用)
        /// </summary>
        public void ShowVoiceRecording(bool isRecording)
        {
            this.isRecording = isRecording;
            
            if (voiceInputIndicator != null)
                voiceInputIndicator.SetActive(isRecording);

            if (isRecording)
            {
                AddSystemMessage("🎤 Recording, release R to send");
            }
            else
            {
                AddSystemMessage("⏹️ Recording ended, processing...");
            }
        }



        /// <summary>
        /// 添加语音消息到聊天界面 (由AudioRecorder调用)
        /// </summary>
        public void AddVoiceMessage(string audioFilePath)
        {
            AddUserMessage("[🎤 Voice Message]");
        }

        // ========== WebSocket集成方法 ==========

        private void SendMessageToAgent(string message)
        {
            // 通过WebSocket发送文字消息
            WsClient.SendTextChatRequest(message);
            
            // 添加系统消息表示正在发送
            AddSystemMessage("💬 Sending text message...");
        }

        /// <summary>
        /// 统一处理Agent回复（语音+文字）
        /// </summary>
        private void OnAgentReplyReceived(AgentReplyMessage response)
        {
            if (response.status != "success")
            {
                AddSystemMessage($"❌ Agent reply failed: {response.status}");
                return;
            }

            // 判断是语音回复还是文字回复
            bool hasTranscription = !string.IsNullOrEmpty(response.transcription);
            bool hasAudio = !string.IsNullOrEmpty(response.audio_path);
            
            // 显示Agent的回复文本
            if (!string.IsNullOrEmpty(response.reply))
            {
                if (hasAudio)
                {
                    // 有音频：添加带音频路径的消息
                    AddAgentMessage(response.reply, response.audio_path);
                }
                else
                {
                    // 纯文字：添加不带音频的消息
                    AddAgentMessage(response.reply);
                }
            }
            else
            {
                AddSystemMessage("⚠️ Agent reply contained no text.");
            }
        }

        private void PlayAgentAudio(ChatMessage message)
        {
            if (!message.HasAudio()) return;
            
            // 这里需要调用现有的音频播放系统
            // 可能是AgentVoiceController的PlayAudio方法
        }

        // ========== 滚动控制方法 ==========

        private void StartAutoScroll()
        {
            if (autoScrollCoroutine != null)
                StopCoroutine(autoScrollCoroutine);
            
            autoScrollCoroutine = StartCoroutine(ScrollToBottom());
        }

        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame(); // 等待布局更新
            
            if (messageScrollRect != null)
            {
                // 平滑滚动到底部
                float targetValue = 0f; // ScrollRect的normalizedPosition.y = 0 表示底部
                
                while (Mathf.Abs(messageScrollRect.verticalNormalizedPosition - targetValue) > 0.01f)
                {
                    messageScrollRect.verticalNormalizedPosition = Mathf.MoveTowards(
                        messageScrollRect.verticalNormalizedPosition, 
                        targetValue, 
                        autoScrollSpeed * Time.deltaTime
                    );
                    yield return null;
                }
                
                messageScrollRect.verticalNormalizedPosition = targetValue;
            }
        }

        // ========== 公共方法 ==========

        /// <summary>
        /// 切换聊天窗口显示/隐藏
        /// </summary>
        public void ToggleChatWindow()
        {
            if (chatWindow != null)
            {
                chatWindow.SetActive(!chatWindow.activeSelf);
            }
        }

        /// <summary>
        /// 清空聊天历史
        /// </summary>
        public void ClearChatHistory()
        {
            // 清空消息历史
            messageHistory.Clear();
            
            // 销毁所有消息UI对象
            foreach (var kvp in messageUIObjects)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            messageUIObjects.Clear();

            // 添加清空提示
            AddSystemMessage("💬 Chat history cleared...");
        }

        /// <summary>
        /// 获取当前消息历史
        /// </summary>
        public List<ChatMessage> GetMessageHistory()
        {
            return new List<ChatMessage>(messageHistory);
        }
        
        // ========== 相机视角切换 ==========
        
        /// <summary>
        /// 切换视角按钮点击事件
        /// </summary>
        private void OnToggleViewClicked()
        {
            UserMainCameraControl cameraControl = ServiceLocator.Get<UserMainCameraControl>();
            if (cameraControl != null)
            {
                cameraControl.ToggleCameraMode();
                
                // 显示切换提示
                string modeName = cameraControl.GetCurrentMode() == UserMainCameraControl.CameraMode.FirstPerson 
                    ? "First Person" : "God View";
                AddSystemMessage($"📷 Switched to {modeName} Mode");
            }
            else
            {
                Debug.LogError("[ChatUIManager] UserMainCameraControl not found!");
            }
        }

    }
}