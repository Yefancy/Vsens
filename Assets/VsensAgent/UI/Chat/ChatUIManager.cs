using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;
using com.convalise.UnityMaterialSymbols;
using VsensAgent.Data;
using VsensAgent.Audio;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;
using VsensAgent.Core;
using VsensAgent.RuntimeEditing;
using TransformHandles;

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
        public Button editModeButton;                   // 运行时编辑模式按钮
        public GameObject voiceInputIndicator;          // 语音录制指示器
        public TransformGizmoUI transformGizmoUI;      // 变换 Gizmo UI
        
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

        [Header("多行输入框")]
        public bool expandInputWhenMultiline = true;    // 多行输入时向上扩展输入区域
        public float inputMinHeight = 60f;              // 输入区域最小高度
        public float inputMaxHeight = 180f;             // 输入区域最大高度，避免吃掉过多历史消息
        public float inputTextVerticalPadding = 24f;    // 输入框内文字上下留白
        public float inputHistoryGap = 10f;             // 输入区域和历史消息区域之间的间距

        [Header("Agent状态显示")]
        public TMP_Text agentStatusText;               // 聊天窗口顶部的状态标签

        // 私有变量
        private List<ChatMessage> messageHistory = new List<ChatMessage>();
        private Dictionary<string, GameObject> messageUIObjects = new Dictionary<string, GameObject>();
        private Coroutine autoScrollCoroutine;
        private AudioRecorder audioRecorder;
        private bool isRecording = false;
        private bool wasInputFocused = false;  // 跟踪输入框聚焦状态
        private Canvas MainCanvas;              // 用于点击检测
        private InputController inputController;      // 输入管理器
        private RuntimeEditModeController runtimeEditModeController;
        private RuntimeTransformHandleBridge runtimeTransformHandleBridge;
        private MaterialSymbol _editModeButtonImage;
        private Color _editModeButtonDefaultColor = Color.white;
        private bool _editModeButtonDefaultColorInitialized;
        private bool _agentIsIdle = true;             // 跟踪Agent是否处于空闲状态（驱动发送/停止按钮）
        private ClarificationRequestMessage _pendingClarification;
        private ProposalReadyMessage _pendingProposal;
        private ChatInteractionOptionsView _activeInteractionOptionsView;
        private Action<string, string[], string> _clarificationReplySender = WsClient.SendClarificationReply;
        private Action<string, string[], string> _proposalSelectSender = WsClient.SendProposalSelect;
        private RectTransform inputAreaRect;
        private RectTransform inputFieldRect;
        private RectTransform messageScrollRectTransform;
        private LayoutElement inputFieldLayoutElement;
        private float baseInputAreaHeight = -1f;
        private float baseMessageScrollBottomInset = -1f;
        private float lastAppliedInputAreaHeight = -1f;
        private float lastMeasuredInputWidth = -1f;
        private const string DefaultInputPlaceholder = "Type your message here... Or R to record voice.";

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
            CacheInputResizeLayout();
            UpdateChatInputLayout();
            runtimeEditModeController = ResolveRuntimeEditModeController(createIfMissing: true);
            runtimeTransformHandleBridge = ResolveRuntimeTransformHandleBridge(createIfMissing: true);
            transformGizmoUI?.Initialize(OnTransformHandleTypeSelected);
            
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
            WsClient.OnAgentReply  += OnAgentReplyReceived;
            WsClient.OnAgentStatus += HandleAgentStatusUI;
            WsClient.OnClarificationRequest += OnClarificationRequestReceived;
            WsClient.OnProposalReady += OnProposalReadyReceived;
            WsClient.OnJobLifecycle += OnJobLifecycleReceived;
            WsClient.OnAgentPush   += OnAgentPushReceived;  // Phase 3: 心跳触发的主动推送
            
            // 从服务定位器获取AudioRecorder引用
            audioRecorder = ServiceLocator.Get<AudioRecorder>();
            if (audioRecorder != null)
            {
                Debug.Log("[ChatUIManager] ✅ AudioRecorder service found");
                // 🔥 同步更新InputController中的AudioRecorder引用
                inputController?.UpdateAudioRecorder(audioRecorder);
            }
            else
            {
                Debug.LogWarning("[ChatUIManager] ⚠️ AudioRecorder not found, voice input disabled");
            }

            // 订阅输入事件
            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendButtonClicked);
            if (textInputField != null)
                textInputField.onValueChanged.AddListener(OnTextInputValueChanged);
            // if (textInputField != null)
                // textInputField.onSubmit.AddListener(OnTextInputSubmit);
            if (toggleViewButton != null)
                toggleViewButton.onClick.AddListener(OnToggleViewClicked);
            if (editModeButton != null)
                editModeButton.onClick.AddListener(OnEditModeClicked);
        }

        void OnDisable()
        {
            // 取消订阅
            WsClient.OnAgentReply  -= OnAgentReplyReceived;
            WsClient.OnAgentStatus -= HandleAgentStatusUI;
            WsClient.OnClarificationRequest -= OnClarificationRequestReceived;
            WsClient.OnProposalReady -= OnProposalReadyReceived;
            WsClient.OnJobLifecycle -= OnJobLifecycleReceived;
            WsClient.OnAgentPush   -= OnAgentPushReceived;  // Phase 3
            
            if (sendButton != null)
                sendButton.onClick.RemoveListener(OnSendButtonClicked);
            if (textInputField != null)
                textInputField.onValueChanged.RemoveListener(OnTextInputValueChanged);
            // if (textInputField != null)
                // textInputField.onSubmit.RemoveListener(OnTextInputSubmit);
            if (toggleViewButton != null)
                toggleViewButton.onClick.RemoveListener(OnToggleViewClicked);
            if (editModeButton != null)
                editModeButton.onClick.RemoveListener(OnEditModeClicked);
        }

        void Update()
        {
            // Keep global input gates in sync with the actual TMP focus state.
            UpdateInputFocusState();

            // 检测输入框聚焦变化
            CheckInputField();
            
            // 处理取消聚焦的输入
            HandleUnfocusInput();
            
            // 修改后的自动聚焦逻辑
            HandleAutoFocus();
            UpdateInputFocusState();
            UpdateChatInputLayoutForWidthChange();
            UpdateEditModeButtonBG();
            UpdateTransformGizmoUI();
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
                UpdateInputPlaceholder();
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
            
            // 初始化状态标签（顶部HUD）
            if (agentStatusText != null)
                agentStatusText.text = "";

            // 初始化发送/停止按钮标签
            UpdateSendButtonLabel();
            UpdateEditModeButtonBG();
            UpdateTransformGizmoUI();

            // 添加欢迎消息
            AddSystemMessage("VsensAgent ready, say hi! Or press R to record voice.");
        }

        private void CacheInputResizeLayout()
        {
            inputFieldRect = textInputField != null ? textInputField.GetComponent<RectTransform>() : null;
            inputAreaRect = inputFieldRect != null ? inputFieldRect.parent as RectTransform : null;
            inputFieldLayoutElement = textInputField != null ? textInputField.GetComponent<LayoutElement>() : null;
            messageScrollRectTransform = messageScrollRect != null ? messageScrollRect.GetComponent<RectTransform>() : null;

            if (inputAreaRect != null && baseInputAreaHeight < 0f)
                baseInputAreaHeight = Mathf.Max(inputMinHeight, inputAreaRect.rect.height);

            if (messageScrollRectTransform != null && baseMessageScrollBottomInset < 0f)
                baseMessageScrollBottomInset = messageScrollRectTransform.offsetMin.y;
        }

        private void OnTextInputValueChanged(string _)
        {
            UpdateChatInputLayout();
        }

        private void UpdateChatInputLayoutForWidthChange()
        {
            float width = GetInputTextMeasureWidth();
            if (width <= 0f || Mathf.Abs(width - lastMeasuredInputWidth) < 0.5f)
                return;

            lastMeasuredInputWidth = width;
            UpdateChatInputLayout();
        }

        private void UpdateChatInputLayout()
        {
            if (!expandInputWhenMultiline || textInputField == null)
                return;

            CacheInputResizeLayout();
            if (inputAreaRect == null)
                return;

            float minHeight = Mathf.Max(1f, inputMinHeight, baseInputAreaHeight);
            float maxHeight = Mathf.Max(minHeight, inputMaxHeight);
            float desiredInputAreaHeight = Mathf.Clamp(CalculateDesiredInputAreaHeight(), minHeight, maxHeight);

            if (Mathf.Abs(desiredInputAreaHeight - lastAppliedInputAreaHeight) < 0.5f)
                return;

            lastAppliedInputAreaHeight = desiredInputAreaHeight;

            inputAreaRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, desiredInputAreaHeight);

            if (inputFieldLayoutElement != null)
            {
                float fieldHeight = Mathf.Max(1f, desiredInputAreaHeight - GetInputAreaVerticalPadding());
                inputFieldLayoutElement.minHeight = fieldHeight;
                inputFieldLayoutElement.preferredHeight = fieldHeight;
            }

            if (messageScrollRectTransform != null)
            {
                Vector2 offsetMin = messageScrollRectTransform.offsetMin;
                offsetMin.y = desiredInputAreaHeight + Mathf.Max(0f, inputHistoryGap);
                if (baseMessageScrollBottomInset >= 0f)
                    offsetMin.y = Mathf.Max(offsetMin.y, baseMessageScrollBottomInset);
                messageScrollRectTransform.offsetMin = offsetMin;
            }

            Canvas.ForceUpdateCanvases();
            textInputField.ForceLabelUpdate();
        }

        private float CalculateDesiredInputAreaHeight()
        {
            float width = GetInputTextMeasureWidth();
            if (width <= 0f || textInputField.textComponent == null)
                return inputMinHeight;

            string measureText = string.IsNullOrEmpty(textInputField.text) ? " " : textInputField.text;
            if (measureText.EndsWith("\n", StringComparison.Ordinal) ||
                measureText.EndsWith("\r", StringComparison.Ordinal))
            {
                measureText += " ";
            }

            Vector2 preferred = textInputField.textComponent.GetPreferredValues(measureText, width, 0f);
            float inputFieldHeight = Mathf.Ceil(preferred.y + Mathf.Max(0f, inputTextVerticalPadding));
            return inputFieldHeight + GetInputAreaVerticalPadding();
        }

        private float GetInputTextMeasureWidth()
        {
            RectTransform textViewport = textInputField != null ? textInputField.textViewport : null;
            if (textViewport != null && textViewport.rect.width > 0f)
                return textViewport.rect.width;

            if (inputFieldRect != null && inputFieldRect.rect.width > 0f)
                return Mathf.Max(1f, inputFieldRect.rect.width - 24f);

            return 0f;
        }

        private float GetInputAreaVerticalPadding()
        {
            var layoutGroup = inputAreaRect != null ? inputAreaRect.GetComponent<HorizontalLayoutGroup>() : null;
            return layoutGroup != null ? layoutGroup.padding.vertical : 0f;
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

        /// <summary>
        /// 统一的发送/停止按钮点击处理。
        /// 当Agent空闲时发送消息；当Agent忙碌时发送硬中断。
        /// </summary>
        private void OnSendButtonClicked()
        {
            if (_agentIsIdle)
                SendTextMessage();
            else
                WsClient.SendAgentInterrupt();
        }

        private void SendTextMessage()
        {
            if (textInputField == null || string.IsNullOrWhiteSpace(textInputField.text)) 
                return;

            string messageContent = textInputField.text.Trim();
            
            // 添加用户消息到聊天界面
            AddUserMessage(messageContent);

            // 优先响应待处理的结构化交互；否则走常规聊天消息
            if (!TrySendPendingInteraction(messageContent))
            {
                SendMessageToAgent(messageContent);
            }

            // 清空输入框
            textInputField.text = "";
            textInputField.Select();
            UpdateInputPlaceholder();
        }

        private void OnTextInputSubmit(string text)
        {
            SendTextMessage();
            // 发送后取消聚焦，让相机控制恢复
            UnfocusInputField();
        }

        private void OnEditModeClicked()
        {
            var controller = ResolveRuntimeEditModeController(createIfMissing: true);
            if (controller == null)
            {
                AddSystemMessage("❌ Runtime edit mode is not available right now.");
                return;
            }

            controller.ToggleEditMode();
            UpdateEditModeButtonBG();
            AddSystemMessage(controller.IsEditModeEnabled
                ? "🛠️ Edit mode enabled. Click the avatar to select it, left-click the floor to move it, and right-drag to rotate."
                : "✅ Edit mode disabled.");
        }

        // ========== 焦点管理方法 ==========

        /// <summary>
        /// 检测输入框聚焦状态变化并通知其他组件
        /// </summary>
        private void UpdateInputFocusState()
        {
            bool isFocused = textInputField != null && textInputField.isFocused;
            if (isFocused == wasInputFocused)
                return;

            wasInputFocused = isFocused;
            inputController?.OnChatFocusChanged(isFocused);
            OnInputFocusChanged?.Invoke(isFocused);
        }

        /// <summary>
        /// 处理输入框自身按键行为
        /// </summary>
        private void CheckInputField()
        {
            if (textInputField == null || !textInputField.isFocused)
                return;

            // 只处理“普通 Enter”
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                bool shift =
                    Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);

                if (shift)
                {
                    // 🔥 关键：完全不要处理，让 TMP 自己走
                    return;
                }

                string text = textInputField.text.TrimEnd('\r', '\n');

                if (string.IsNullOrWhiteSpace(text))
                    return;

                SendTextMessage();

                textInputField.text = "";
                textInputField.ActivateInputField();
                textInputField.MoveTextEnd(false);
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
            if (ResolveRuntimeEditModeController(createIfMissing: false)?.IsEditModeEnabled == true) return;
            
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

        // ========== Agent状态UI ==========

        /// <summary>
        /// 处理Python端主动推送的Agent状态变化（Phase 1）。
        /// 更新顶部状态标签，并在空闲/忙碌之间切换发送/停止按钮。
        /// </summary>
        private void HandleAgentStatusUI(AgentStatusMessage msg)
        {
            if (msg == null) return;

            _agentIsIdle = msg.state == "idle";

            if (agentStatusText != null)
                agentStatusText.text = AgentStateToDisplayString(msg.state);

            UpdateSendButtonLabel();
        }

        /// <summary>
        /// 更新发送/停止按钮上的标签文字。
        /// </summary>
        private void UpdateSendButtonLabel()
        {
            if (sendButton == null) return;
            var label = sendButton.GetComponentInChildren<TMP_Text>();
            if (label == null) return;
            label.text = _agentIsIdle ? "OK" : "Stop";
        }

        private void UpdateEditModeButtonBG()
        {
            if (editModeButton == null) {
                return;
            }

            if (_editModeButtonImage == null)
            {
                _editModeButtonImage = editModeButton.GetComponent<MaterialSymbol>();
            }

            if (_editModeButtonImage == null)
            {
                return;
            }

            if (!_editModeButtonDefaultColorInitialized)
            {
                _editModeButtonDefaultColor = _editModeButtonImage.color;
                _editModeButtonDefaultColorInitialized = true;
            }

            bool isEditModeEnabled = ResolveRuntimeEditModeController(createIfMissing: false)?.IsEditModeEnabled == true;
            _editModeButtonImage.color = isEditModeEnabled
                ? new Color(0.32f, 0.78f, 0.32f, _editModeButtonDefaultColor.a)
                : _editModeButtonDefaultColor;
        }

        private void UpdateTransformGizmoUI()
        {
            if (transformGizmoUI == null)
            {
                return;
            }

            runtimeEditModeController = ResolveRuntimeEditModeController(createIfMissing: false);
            runtimeTransformHandleBridge = ResolveRuntimeTransformHandleBridge(createIfMissing: false);

            bool shouldShow = runtimeEditModeController != null
                && runtimeEditModeController.IsEditModeEnabled
                && !string.IsNullOrWhiteSpace(runtimeEditModeController.SelectedObjectId)
                && runtimeTransformHandleBridge != null
                && runtimeTransformHandleBridge.SupportsCurrentSelection;

            if (transformGizmoUI.gameObject.activeSelf != shouldShow)
            {
                transformGizmoUI.gameObject.SetActive(shouldShow);
            }

            if (shouldShow)
            {
                transformGizmoUI.SetMode(runtimeTransformHandleBridge.CurrentHandleType);
            }
        }

        private void OnTransformHandleTypeSelected(HandleType handleType)
        {
            runtimeTransformHandleBridge = ResolveRuntimeTransformHandleBridge(createIfMissing: true);
            runtimeTransformHandleBridge?.SetHandleType(handleType);
            UpdateTransformGizmoUI();
        }

        private RuntimeEditModeController ResolveRuntimeEditModeController(bool createIfMissing)
        {
            if (runtimeEditModeController != null)
            {
                return runtimeEditModeController;
            }

            runtimeEditModeController = ServiceLocator.Get<RuntimeEditModeController>() ?? FindFirstObjectByType<RuntimeEditModeController>();
            if (runtimeEditModeController != null || !createIfMissing)
            {
                return runtimeEditModeController;
            }

            var runtime = ServiceLocator.Get<VsensAgent.SceneApi.V2.AvatarRuntimeManager>() ?? FindFirstObjectByType<VsensAgent.SceneApi.V2.AvatarRuntimeManager>();
            GameObject host = runtime != null ? runtime.gameObject : new GameObject("RuntimeEditModeController");
            runtimeEditModeController = host.GetComponent<RuntimeEditModeController>();
            if (runtimeEditModeController == null)
            {
                runtimeEditModeController = host.AddComponent<RuntimeEditModeController>();
            }

            if (runtime != null)
            {
                runtimeEditModeController.Configure(runtime);
            }

            return runtimeEditModeController;
        }

        private RuntimeTransformHandleBridge ResolveRuntimeTransformHandleBridge(bool createIfMissing)
        {
            if (runtimeTransformHandleBridge != null)
            {
                return runtimeTransformHandleBridge;
            }

            var controller = ResolveRuntimeEditModeController(createIfMissing);
            if (controller == null)
            {
                return null;
            }

            runtimeTransformHandleBridge = controller.GetComponent<RuntimeTransformHandleBridge>();
            if (runtimeTransformHandleBridge == null && createIfMissing)
            {
                runtimeTransformHandleBridge = controller.gameObject.AddComponent<RuntimeTransformHandleBridge>();
            }

            return runtimeTransformHandleBridge;
        }

        /// <summary>
        /// 将Python端的状态字符串映射为UI友好的显示文本。
        /// </summary>
        private static string AgentStateToDisplayString(string state)
        {
            switch (state)
            {
                case "idle":         return "✅ Idle";
                case "listening":    return "🎤 Listening";
                case "transcribing": return "✍️ Transcribing";
                case "thinking":     return "💭 Thinking";
                case "planning":     return "📋 Planning";
                case "executing":    return "⚙️ Executing";
                case "speaking":     return "🔊 Speaking";
                case "waiting":      return "⏳ Waiting";
                case "scripting":    return "📝 Scripting";
                default:             return state;
            }
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
            var audioMgr = ServiceLocator.Get<AgentAudioManager>();
            if (audioMgr != null)
                audioMgr.PlayNow(message.audioPath);
            else
                Debug.LogWarning("[ChatUI] AgentAudioManager not found — cannot replay audio.");
        }

        /// <summary>
        /// Phase 3: 处理 HeartbeatHandler 心跳触发的主动推送。
        /// Python 端仅在 severity >= 0.5 时才发送，所以此处收到的消息必然是具有实际意义的事件。
        /// </summary>
        private void OnAgentPushReceived(AgentPushMessage push)
        {
            if (push == null) return;

            // 显示 Agent 主动说话内容（无转录脸）
            if (!string.IsNullOrEmpty(push.reply))
            {
                // 前缀 🔔 让用户能区分主动推送和普通回复
                // Pass audio_path when Python TTS is active (tts_in_push=true in server_config)
                AddAgentMessage("🔔 " + push.reply, push.audio_path ?? "");
            }
        }

        private void OnClarificationRequestReceived(ClarificationRequestMessage request)
        {
            if (request == null)
            {
                return;
            }

            DisableActiveInteractionOptions();
            _pendingClarification = request;
            _pendingProposal = null;
            AddSystemMessage(FormatClarificationRequest(request));
            AttachClarificationOptionsToLatestMessage(request);
            UpdateInputPlaceholder();
            FocusInputField();
        }

        private void OnProposalReadyReceived(ProposalReadyMessage proposal)
        {
            if (proposal == null)
            {
                return;
            }

            DisableActiveInteractionOptions();
            _pendingProposal = proposal;
            _pendingClarification = null;
            AddSystemMessage(FormatProposalReady(proposal));
            AttachProposalOptionsToLatestMessage(proposal);
            UpdateInputPlaceholder();
            FocusInputField();
        }

        private void OnJobLifecycleReceived(JobLifecycleMessage job)
        {
            if (job == null)
            {
                return;
            }

            AddSystemMessage(FormatJobLifecycle(job));
        }

        private bool TrySendPendingInteraction(string messageContent)
        {
            if (_pendingClarification != null)
            {
                var selectedIds = TryParseClarificationSelection(messageContent);
                var freeText = selectedIds.Length > 0 ? string.Empty : messageContent;
                _clarificationReplySender(_pendingClarification.question_id, selectedIds, freeText);
                _pendingClarification = null;
                DisableActiveInteractionOptions();
                UpdateInputPlaceholder();
                return true;
            }

            if (_pendingProposal != null)
            {
                var selectedIds = TryParseProposalSelection(messageContent);
                var note = selectedIds.Length > 0 ? string.Empty : messageContent;
                _proposalSelectSender(_pendingProposal.proposal_id, selectedIds, note);
                _pendingProposal = null;
                DisableActiveInteractionOptions();
                UpdateInputPlaceholder();
                return true;
            }

            return false;
        }

        private string[] TryParseClarificationSelection(string input)
        {
            if (_pendingClarification == null || _pendingClarification.options == null)
            {
                return Array.Empty<string>();
            }

            var optionIds = new string[_pendingClarification.options.Length];
            var optionLabels = new string[_pendingClarification.options.Length];
            for (var index = 0; index < _pendingClarification.options.Length; index++)
            {
                optionIds[index] = _pendingClarification.options[index].id;
                optionLabels[index] = _pendingClarification.options[index].label;
            }

            return ChatProtocolInputParser.TryParseSelection(
                input,
                optionIds,
                optionLabels,
                string.Equals(_pendingClarification.selection_mode, "multiple", StringComparison.OrdinalIgnoreCase),
                out var selectedIds
            ) ? selectedIds : Array.Empty<string>();
        }

        private string[] TryParseProposalSelection(string input)
        {
            if (_pendingProposal == null || _pendingProposal.options == null)
            {
                return Array.Empty<string>();
            }

            var optionIds = new string[_pendingProposal.options.Length];
            var optionLabels = new string[_pendingProposal.options.Length];
            for (var index = 0; index < _pendingProposal.options.Length; index++)
            {
                optionIds[index] = _pendingProposal.options[index].id;
                optionLabels[index] = _pendingProposal.options[index].label;
            }

            return ChatProtocolInputParser.TryParseSelection(
                input,
                optionIds,
                optionLabels,
                false,
                out var selectedIds
            ) ? selectedIds : Array.Empty<string>();
        }

        private string FormatClarificationRequest(ClarificationRequestMessage request)
        {
            var lines = new List<string>
            {
                $"Clarification requested: {request.prompt}"
            };

            if (request.options != null)
            {
                for (var index = 0; index < request.options.Length; index++)
                {
                    var option = request.options[index];
                    var suffix = string.IsNullOrWhiteSpace(option.description)
                        ? string.Empty
                        : $" - {option.description}";
                    lines.Add($"{index + 1}. {option.label}{suffix}");
                }
            }

            lines.Add(
                string.Equals(request.selection_mode, "multiple", StringComparison.OrdinalIgnoreCase)
                    ? "Reply with option numbers, ids, or labels. You can separate multiple choices with commas."
                    : "Reply with an option number, id, label, or type your own answer."
            );

            return string.Join("\n", lines);
        }

        private string FormatProposalReady(ProposalReadyMessage proposal)
        {
            var lines = new List<string>
            {
                $"Proposal ready: {proposal.title}"
            };

            if (!string.IsNullOrWhiteSpace(proposal.summary))
            {
                lines.Add(proposal.summary);
            }

            if (proposal.options != null)
            {
                for (var index = 0; index < proposal.options.Length; index++)
                {
                    var option = proposal.options[index];
                    var suffix = string.IsNullOrWhiteSpace(option.description)
                        ? string.Empty
                        : $" - {option.description}";
                    lines.Add($"{index + 1}. {option.label}{suffix}");
                }
            }

            lines.Add("Reply with an option number, id, label, or type a note to refine the proposal.");
            return string.Join("\n", lines);
        }

        private void AttachClarificationOptionsToLatestMessage(ClarificationRequestMessage request)
        {
            if (request.options == null || request.options.Length == 0)
            {
                return;
            }

            var optionsView = AttachInteractionOptionsViewToLatestMessage();
            if (optionsView == null)
            {
                return;
            }

            var optionData = new ChatInteractionOptionData[request.options.Length];
            for (var index = 0; index < request.options.Length; index++)
            {
                var option = request.options[index];
                optionData[index] = new ChatInteractionOptionData(option.id, option.label, option.description);
            }

            optionsView.Initialize(
                optionData,
                string.Equals(request.selection_mode, "multiple", StringComparison.OrdinalIgnoreCase),
                OnClarificationOptionsSubmitted,
                ResolveMessageFontAsset(optionsView.transform.parent));
            _activeInteractionOptionsView = optionsView;
        }

        private void AttachProposalOptionsToLatestMessage(ProposalReadyMessage proposal)
        {
            if (proposal.options == null || proposal.options.Length == 0)
            {
                return;
            }

            var optionsView = AttachInteractionOptionsViewToLatestMessage();
            if (optionsView == null)
            {
                return;
            }

            var optionData = new ChatInteractionOptionData[proposal.options.Length];
            for (var index = 0; index < proposal.options.Length; index++)
            {
                var option = proposal.options[index];
                optionData[index] = new ChatInteractionOptionData(option.id, option.label, option.description);
            }

            optionsView.Initialize(
                optionData,
                allowMultiple: false,
                OnProposalOptionsSubmitted,
                ResolveMessageFontAsset(optionsView.transform.parent));
            _activeInteractionOptionsView = optionsView;
        }

        private ChatInteractionOptionsView AttachInteractionOptionsViewToLatestMessage()
        {
            if (messageContainer == null || messageContainer.childCount == 0)
            {
                return null;
            }

            var latestMessage = messageContainer.GetChild(messageContainer.childCount - 1);
            var optionsRoot = new GameObject("InteractionOptions", typeof(RectTransform));
            optionsRoot.transform.SetParent(latestMessage, false);

            var rectTransform = optionsRoot.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.sizeDelta = Vector2.zero;

            return optionsRoot.AddComponent<ChatInteractionOptionsView>();
        }

        private TMP_FontAsset ResolveMessageFontAsset(Transform messageRoot)
        {
            if (messageRoot == null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            var text = messageRoot.GetComponentInChildren<TextMeshProUGUI>();
            return text != null ? text.font : TMP_Settings.defaultFontAsset;
        }

        private void OnClarificationOptionsSubmitted(string[] selectedIds)
        {
            if (_pendingClarification == null)
            {
                return;
            }

            _clarificationReplySender(_pendingClarification.question_id, selectedIds ?? Array.Empty<string>(), string.Empty);
            _pendingClarification = null;
            DisableActiveInteractionOptions();
            UpdateInputPlaceholder();
        }

        private void OnProposalOptionsSubmitted(string[] selectedIds)
        {
            if (_pendingProposal == null)
            {
                return;
            }

            _proposalSelectSender(_pendingProposal.proposal_id, selectedIds ?? Array.Empty<string>(), string.Empty);
            _pendingProposal = null;
            DisableActiveInteractionOptions();
            UpdateInputPlaceholder();
        }

        private void DisableActiveInteractionOptions()
        {
            if (_activeInteractionOptionsView == null)
            {
                return;
            }

            _activeInteractionOptionsView.SetInteractable(false);
            _activeInteractionOptionsView = null;
        }

        private void UpdateInputPlaceholder()
        {
            if (textInputField == null || textInputField.placeholder == null)
            {
                return;
            }

            var placeholderText = textInputField.placeholder.GetComponent<TextMeshProUGUI>();
            if (placeholderText == null)
            {
                return;
            }

            if (_pendingClarification != null)
            {
                placeholderText.text = "Choose a clarification option or type an alternative.";
                return;
            }

            if (_pendingProposal != null)
            {
                placeholderText.text = "Choose a proposal option or type a note.";
                return;
            }

            placeholderText.text = DefaultInputPlaceholder;
        }

        private string FormatJobLifecycle(JobLifecycleMessage job)
        {
            switch (job.type)
            {
                case "job.started":
                    return $"Job started: {job.job_kind} ({job.job_id})";
                case "job.cancelled":
                    return string.IsNullOrWhiteSpace(job.reason)
                        ? $"Job cancelled: {job.job_kind} ({job.job_id})"
                        : $"Job cancelled: {job.job_kind} ({job.job_id}) - {job.reason}";
                default:
                    return $"Job status: {job.job_kind} ({job.job_id}) - {job.status}";
            }
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
            _pendingClarification = null;
            _pendingProposal = null;
            _activeInteractionOptionsView = null;
            UpdateInputPlaceholder();
            
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
