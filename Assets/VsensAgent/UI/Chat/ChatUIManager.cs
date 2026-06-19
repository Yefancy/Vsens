using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using com.convalise.UnityMaterialSymbols;
using TMPro;
using TransformHandles;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using VsensAgent.Audio;
using VsensAgent.Core;
using VsensAgent.Data;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;
using VsensAgent.RuntimeEditing;
using UIButton = UnityEngine.UI.Button;

namespace VsensAgent.UI
{
    public class ChatUIManager : MonoBehaviour
    {
        public UIDocument uiDocument;

        [Header("UI References")]
        public UIButton toggleViewButton;
        public UIButton editModeButton;
        public GameObject voiceInputIndicator;
        public TransformGizmoUI transformGizmoUI;

        [Header("Settings")]
        public int maxMessageHistory = Constants.UI.MAX_CHAT_HISTORY;
        public float autoScrollSpeed = 1f;
        public bool autoFocusInput = true;

        [Header("Interaction")]
        public bool disableCameraWhenFocused = true;
        public KeyCode unfocusKey = Constants.InputKeys.UNFOCUS;

        [Header("Agent Status")]
        public TMP_Text agentStatusText;

        [Header("Markdown")]
        [SerializeField] private ViewsCollection markdownViewsCollection;

        private readonly List<ChatMessage> messageHistory = new();
        private readonly Dictionary<string, VisualElement> messageUIElements = new();
        private readonly Dictionary<string, string> _delegatedTaskMessageIdsByJobId = new();
        private readonly Dictionary<string, List<LocalDelegatedTaskArtifact>> _delegatedTaskArtifactsByJobId = new();

        private Coroutine autoScrollCoroutine;
        private AudioRecorder audioRecorder;
        private bool isRecording;
        private bool wasInputFocused;
        private InputController inputController;
        private RuntimeEditModeController runtimeEditModeController;
        private RuntimeTransformHandleBridge runtimeTransformHandleBridge;
        private MaterialSymbol _editModeButtonImage;
        private Color _editModeButtonDefaultColor = Color.white;
        private bool _editModeButtonDefaultColorInitialized;
        private bool _agentIsIdle = true;
        private ClarificationRequestMessage _pendingClarification;
        private ProposalReadyMessage _pendingProposal;
        private ChatInteractionOptionsElement _activeInteractionOptionsElement;
        private Action<string, string[], string> _clarificationReplySender = WsClient.SendClarificationReply;
        private Action<string, string[], string> _proposalSelectSender = WsClient.SendProposalSelect;
        private ChatPanelView _chatPanelView;
        private bool _uiToolkitEventsBound;

        private const string DefaultInputPlaceholder = "Type your message here...";

        [Serializable]
        private class LocalDelegatedTaskArtifact
        {
            public string fileName;
            public string kind;
            public string label;
            public string serverPath;
            public string localPath;
        }

        public static Action<string> OnUserMessageSent;
        public static Action<ChatMessage> OnMessageAdded;
        public static Action<bool> OnInputFocusChanged;

        private void Awake()
        {
            EnsureChatPanelView();

            runtimeEditModeController = ResolveRuntimeEditModeController(createIfMissing: true);
            runtimeTransformHandleBridge = ResolveRuntimeTransformHandleBridge(createIfMissing: true);
            transformGizmoUI?.Initialize(OnTransformHandleTypeSelected);

            inputController = new InputController();
            inputController.Initialize();

            InitializeUI();
            ServiceLocator.Register<ChatUIManager>(this);
        }

        private void OnEnable()
        {
            WsClient.OnAgentReply += OnAgentReplyReceived;
            WsClient.OnAgentStatus += HandleAgentStatusUI;
            WsClient.OnClarificationRequest += OnClarificationRequestReceived;
            WsClient.OnProposalReady += OnProposalReadyReceived;
            WsClient.OnJobLifecycle += OnJobLifecycleReceived;
            WsClient.OnDelegatedTaskResult += OnDelegatedTaskResultReceived;
            WsClient.OnDelegatedTaskArtifactsSnapshot += OnDelegatedTaskArtifactsSnapshotReceived;
            WsClient.OnAgentPush += OnAgentPushReceived;

            audioRecorder = ServiceLocator.Get<AudioRecorder>();
            inputController?.UpdateAudioRecorder(audioRecorder);

            if (toggleViewButton != null)
                toggleViewButton.onClick.AddListener(OnToggleViewClicked);
            if (editModeButton != null)
                editModeButton.onClick.AddListener(OnEditModeClicked);

            EnsureChatPanelView();
            BindUiToolkitEvents();
        }

        private void OnDisable()
        {
            WsClient.OnAgentReply -= OnAgentReplyReceived;
            WsClient.OnAgentStatus -= HandleAgentStatusUI;
            WsClient.OnClarificationRequest -= OnClarificationRequestReceived;
            WsClient.OnProposalReady -= OnProposalReadyReceived;
            WsClient.OnJobLifecycle -= OnJobLifecycleReceived;
            WsClient.OnDelegatedTaskResult -= OnDelegatedTaskResultReceived;
            WsClient.OnDelegatedTaskArtifactsSnapshot -= OnDelegatedTaskArtifactsSnapshotReceived;
            WsClient.OnAgentPush -= OnAgentPushReceived;

            if (toggleViewButton != null)
                toggleViewButton.onClick.RemoveListener(OnToggleViewClicked);
            if (editModeButton != null)
                editModeButton.onClick.RemoveListener(OnEditModeClicked);

            UnbindUiToolkitEvents();
        }

        private void Update()
        {
            EnsureChatPanelView();
            UpdateInputFocusState();
            CheckInputField();
            HandleUnfocusInput();
            HandleAutoFocus();
            UpdateInputFocusState();
            UpdateEditModeButtonBG();
            UpdateTransformGizmoUI();
        }

        private bool EnsureChatPanelView()
        {
            if (_chatPanelView != null && _chatPanelView.IsReady)
                return true;

            uiDocument ??= GetComponentInChildren<UIDocument>(true);
            if (uiDocument == null || uiDocument.rootVisualElement == null)
                return false;

            var candidate = new ChatPanelView(uiDocument.rootVisualElement);
            if (!candidate.IsReady)
                return false;

            _chatPanelView = candidate;
            markdownViewsCollection ??= Resources.Load<ViewsCollection>("VsensAgent/ChatMarkdownViews");
            BindUiToolkitEvents();
            UpdateInputPlaceholder();
            UpdateSendButtonLabel();
            RebuildUiToolkitMessages();
            _chatPanelView.SetPanelVisible(true);
            return true;
        }

        private void BindUiToolkitEvents()
        {
            if (_uiToolkitEventsBound || _chatPanelView == null || !_chatPanelView.IsReady)
                return;

            _chatPanelView.SendButton.clicked += OnSendButtonClicked;
            _uiToolkitEventsBound = true;
        }

        private void UnbindUiToolkitEvents()
        {
            if (!_uiToolkitEventsBound || _chatPanelView == null || !_chatPanelView.IsReady)
                return;

            _chatPanelView.SendButton.clicked -= OnSendButtonClicked;
            _uiToolkitEventsBound = false;
        }

        private void InitializeUI()
        {
            if (voiceInputIndicator != null)
                voiceInputIndicator.SetActive(false);

            EnsureChatPanelView();
            if (_chatPanelView != null && _chatPanelView.IsReady)
                _chatPanelView.InputField.value = string.Empty;

            if (agentStatusText != null)
                agentStatusText.text = string.Empty;

            UpdateInputPlaceholder();
            UpdateSendButtonLabel();
            UpdateEditModeButtonBG();
            UpdateTransformGizmoUI();

            if (messageHistory.Count == 0)
                AddSystemMessage("VsensAgent ready, say hi! Or press R to record voice.");
        }

        private ChatMessage AddMessage(ChatMessage message)
        {
            if (message == null)
                return null;

            messageHistory.Add(message);
            if (messageHistory.Count > maxMessageHistory)
            {
                var removed = messageHistory[0];
                messageHistory.RemoveAt(0);
                RemoveMessageUi(removed.messageId);
            }

            CreateMessageUI(message);
            StartAutoScroll();
            OnMessageAdded?.Invoke(message);
            return message;
        }

        public ChatMessage AddUserMessage(string content)
        {
            var message = new ChatMessage(content, ChatMessage.MessageType.User);
            AddMessage(message);
            OnUserMessageSent?.Invoke(content);
            return message;
        }

        public ChatMessage AddAgentMessage(string content, string audioPath = "")
        {
            var message = new ChatMessage(content, ChatMessage.MessageType.Agent, audioPath);
            return AddMessage(message);
        }

        public ChatMessage AddSystemMessage(string content)
        {
            var message = new ChatMessage(content, ChatMessage.MessageType.System);
            return AddMessage(message);
        }

        private void RemoveMessageUi(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return;

            if (messageUIElements.TryGetValue(messageId, out var element))
            {
                element.RemoveFromHierarchy();
                messageUIElements.Remove(messageId);
            }
        }

        private void RebuildUiToolkitMessages()
        {
            if (_chatPanelView == null || !_chatPanelView.IsReady)
                return;

            _chatPanelView.ClearMessages();
            messageUIElements.Clear();

            foreach (var message in messageHistory)
                CreateUiToolkitMessage(message);
        }

        private void CreateMessageUI(ChatMessage message)
        {
            EnsureChatPanelView();
            if (_chatPanelView == null || !_chatPanelView.IsReady)
                return;

            CreateUiToolkitMessage(message);
        }

        private void CreateUiToolkitMessage(ChatMessage message)
        {
            var element = ChatMessageElementFactory.CreateMessageElement(message, PlayAgentAudio, markdownViewsCollection);
            messageUIElements[message.messageId] = element;
            _chatPanelView.AddMessageElement(element);
        }

        private void OnSendButtonClicked()
        {
            if (_agentIsIdle || _pendingClarification != null || _pendingProposal != null)
                SendTextMessage();
            else
                WsClient.SendAgentInterrupt();
        }

        private void SendTextMessage(string messageContentOverride = null)
        {
            var messageContent = string.IsNullOrWhiteSpace(messageContentOverride)
                ? GetInputText().Trim()
                : messageContentOverride.Trim();
            if (string.IsNullOrWhiteSpace(messageContent))
                return;

            AddUserMessage(messageContent);

            if (!TrySendPendingInteraction(messageContent))
                SendMessageToAgent(messageContent);

            SetInputText(string.Empty);
            FocusInputField();
            UpdateInputPlaceholder();
        }

        private string GetInputText()
        {
            return _chatPanelView != null && _chatPanelView.IsReady
                ? _chatPanelView.InputField.value ?? string.Empty
                : string.Empty;
        }

        private void SetInputText(string value)
        {
            if (_chatPanelView != null && _chatPanelView.IsReady)
                _chatPanelView.InputField.value = value ?? string.Empty;
        }

        private void UpdateInputFocusState()
        {
            bool isFocused = IsInputFocused();
            if (isFocused == wasInputFocused)
                return;

            wasInputFocused = isFocused;
            if (disableCameraWhenFocused)
                inputController?.OnChatFocusChanged(isFocused);
            OnInputFocusChanged?.Invoke(isFocused);
        }

        private void CheckInputField()
        {
            if (!IsInputFocused())
                return;

            if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
                return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift)
            {
                AppendInputNewline();
                return;
            }

            var text = GetInputText().TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (_agentIsIdle || _pendingClarification != null || _pendingProposal != null)
            {
                SendTextMessage(text);
                StartCoroutine(ClearInputFieldNextFrame());
                return;
            }

            WsClient.SendAgentInterrupt();
        }

        private void AppendInputNewline()
        {
            var current = GetInputText();
            SetInputText(string.IsNullOrEmpty(current) ? "\n" : $"{current}\n");
            FocusInputField();
        }

        private IEnumerator ClearInputFieldNextFrame()
        {
            yield return null;
            SetInputText(string.Empty);
            FocusInputField();
        }

        private void HandleUnfocusInput()
        {
            if (!Input.GetKeyDown(unfocusKey) || !IsInputFocused())
                return;

            UnfocusInputField();
        }

        private void HandleAutoFocus()
        {
            if (!autoFocusInput || isRecording || IsInputFocused())
                return;
            if (ResolveRuntimeEditModeController(createIfMissing: false)?.IsEditModeEnabled == true)
                return;

            if (!IsAnyCameraMovementKeyPressedForAutoFocus())
                FocusInputField();
        }

        private static bool IsAnyCameraMovementKeyPressedForAutoFocus()
        {
            return Input.GetKey(KeyCode.W) ||
                   Input.GetKey(KeyCode.A) ||
                   Input.GetKey(KeyCode.S) ||
                   Input.GetKey(KeyCode.D) ||
                   Input.GetKey(KeyCode.Space) ||
                   Input.GetKey(KeyCode.C) ||
                   Input.GetKey(KeyCode.LeftShift) ||
                   Input.GetKey(KeyCode.RightShift);
        }

        public static bool IsCameraMovementKeyForAutoFocus(KeyCode key)
        {
            return key == KeyCode.W ||
                   key == KeyCode.A ||
                   key == KeyCode.S ||
                   key == KeyCode.D ||
                   key == KeyCode.Space ||
                   key == KeyCode.C ||
                   key == KeyCode.LeftShift ||
                   key == KeyCode.RightShift;
        }

        public bool IsInputFocused()
        {
            return _chatPanelView != null && _chatPanelView.IsReady && _chatPanelView.IsInputFocused;
        }

        public void FocusInputField()
        {
            if (_chatPanelView != null && _chatPanelView.IsReady)
                _chatPanelView.FocusInput();
        }

        private void UnfocusInputField()
        {
            if (_chatPanelView != null && _chatPanelView.IsReady)
                _chatPanelView.BlurInput();
        }

        public void ShowVoiceRecording(bool recording)
        {
            isRecording = recording;
            if (voiceInputIndicator != null)
                voiceInputIndicator.SetActive(recording);

            AddSystemMessage(recording
                ? "🎤 Recording, release R to send"
                : "⏹️ Recording ended, processing...");
        }

        public void AddVoiceMessage(string audioFilePath)
        {
            AddUserMessage("[🎤 Voice Message]");
        }

        private void HandleAgentStatusUI(AgentStatusMessage msg)
        {
            if (msg == null)
                return;

            _agentIsIdle = msg.state == "idle";
            if (agentStatusText != null)
                agentStatusText.text = AgentStateToDisplayString(msg.state);

            UpdateSendButtonLabel();
        }

        private void UpdateSendButtonLabel()
        {
            // string label = _agentIsIdle ? "Send" : "Stop";
            //
            // if (_chatPanelView != null && _chatPanelView.IsReady)
            //     _chatPanelView.SetSendButtonText(label, !_agentIsIdle);
        }

        private static string AgentStateToDisplayString(string state)
        {
            return state switch
            {
                "idle" => "✅ Idle",
                "listening" => "🎤 Listening",
                "transcribing" => "✍️ Transcribing",
                "thinking" => "💭 Thinking",
                "planning" => "📋 Planning",
                "executing" => "⚙️ Executing",
                "speaking" => "🔊 Speaking",
                "waiting" => "⏳ Waiting",
                "waiting_subagent" => "⏳ Waiting For Worker",
                "scripting" => "📝 Scripting",
                _ => state
            };
        }

        private void SendMessageToAgent(string message)
        {
            WsClient.SendTextChatRequest(message);
            AddSystemMessage("💬 Sending text message...");
        }

        private void OnAgentReplyReceived(AgentReplyMessage response)
        {
            if (response == null)
                return;

            if (response.status != "success")
            {
                AddSystemMessage($"❌ Agent reply failed: {response.status}");
                return;
            }

            if (!string.IsNullOrEmpty(response.reply))
                AddAgentMessage(response.reply, response.audio_path ?? string.Empty);
            else
                AddSystemMessage("⚠️ Agent reply contained no text.");
        }

        private void PlayAgentAudio(ChatMessage message)
        {
            if (message == null || !message.HasAudio())
                return;

            var audioMgr = ServiceLocator.Get<AgentAudioManager>();
            if (audioMgr != null)
                audioMgr.PlayNow(message.audioPath);
            else
                Debug.LogWarning("[ChatUI] AgentAudioManager not found — cannot replay audio.");
        }

        private void OnAgentPushReceived(AgentPushMessage push)
        {
            if (push == null || string.IsNullOrEmpty(push.reply))
                return;

            AddAgentMessage("🔔 " + push.reply, push.audio_path ?? string.Empty);
        }

        private void OnClarificationRequestReceived(ClarificationRequestMessage request)
        {
            if (request == null)
                return;

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
                return;

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
            if (job != null)
                AddSystemMessage(FormatJobLifecycle(job));
        }

        private void OnDelegatedTaskResultReceived(DelegatedTaskResultMessage result)
        {
            if (result == null)
                return;

            var message = AddAgentMessage(FormatDelegatedTaskResult(result));
            if (message != null && !string.IsNullOrWhiteSpace(result.job_id))
            {
                _delegatedTaskMessageIdsByJobId[result.job_id] = message.messageId;
                AttachDelegatedTaskArtifactsToMessage(result.job_id);
            }
        }

        private void OnDelegatedTaskArtifactsSnapshotReceived(DelegatedTaskArtifactsSnapshotMessage snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.job_id))
                return;

            var localArtifacts = PersistDelegatedTaskArtifacts(snapshot);
            if (localArtifacts.Count == 0)
                return;

            _delegatedTaskArtifactsByJobId[snapshot.job_id] = localArtifacts;
            AttachDelegatedTaskArtifactsToMessage(snapshot.job_id);
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
            if (_pendingClarification?.options == null)
                return Array.Empty<string>();

            var optionIds = new string[_pendingClarification.options.Length];
            var optionLabels = new string[_pendingClarification.options.Length];
            for (int index = 0; index < _pendingClarification.options.Length; index++)
            {
                optionIds[index] = _pendingClarification.options[index].id;
                optionLabels[index] = _pendingClarification.options[index].label;
            }

            return ChatProtocolInputParser.TryParseSelection(
                input,
                optionIds,
                optionLabels,
                string.Equals(_pendingClarification.selection_mode, "multiple", StringComparison.OrdinalIgnoreCase),
                out var selectedIds)
                ? selectedIds
                : Array.Empty<string>();
        }

        private string[] TryParseProposalSelection(string input)
        {
            if (_pendingProposal?.options == null)
                return Array.Empty<string>();

            var optionIds = new string[_pendingProposal.options.Length];
            var optionLabels = new string[_pendingProposal.options.Length];
            for (int index = 0; index < _pendingProposal.options.Length; index++)
            {
                optionIds[index] = _pendingProposal.options[index].id;
                optionLabels[index] = _pendingProposal.options[index].label;
            }

            return ChatProtocolInputParser.TryParseSelection(
                input,
                optionIds,
                optionLabels,
                allowMultiple: false,
                out var selectedIds)
                ? selectedIds
                : Array.Empty<string>();
        }

        private string FormatClarificationRequest(ClarificationRequestMessage request)
        {
            var lines = new List<string> { $"Clarification requested: {request.prompt}" };
            if (request.options != null)
            {
                for (int index = 0; index < request.options.Length; index++)
                {
                    var option = request.options[index];
                    var suffix = string.IsNullOrWhiteSpace(option.description) ? string.Empty : $" - {option.description}";
                    lines.Add($"{index + 1}. {option.label}{suffix}");
                }
            }

            lines.Add(string.Equals(request.selection_mode, "multiple", StringComparison.OrdinalIgnoreCase)
                ? "Reply with option numbers, ids, or labels. You can separate multiple choices with commas."
                : "Reply with an option number, id, label, or type your own answer.");
            return string.Join("\n", lines);
        }

        private string FormatProposalReady(ProposalReadyMessage proposal)
        {
            var lines = new List<string> { $"Proposal ready: {proposal.title}" };
            if (!string.IsNullOrWhiteSpace(proposal.summary))
                lines.Add(proposal.summary);

            if (proposal.options != null)
            {
                for (int index = 0; index < proposal.options.Length; index++)
                {
                    var option = proposal.options[index];
                    var suffix = string.IsNullOrWhiteSpace(option.description) ? string.Empty : $" - {option.description}";
                    lines.Add($"{index + 1}. {option.label}{suffix}");
                }
            }

            lines.Add("Reply with an option number, id, label, or type a note to refine the proposal.");
            return string.Join("\n", lines);
        }

        private void AttachClarificationOptionsToLatestMessage(ClarificationRequestMessage request)
        {
            if (request?.options == null || request.options.Length == 0)
                return;

            var latestMessage = GetLatestRenderedMessageElement();
            if (latestMessage == null)
                return;

            var optionData = new ChatInteractionOptionData[request.options.Length];
            for (int index = 0; index < request.options.Length; index++)
            {
                var option = request.options[index];
                optionData[index] = new ChatInteractionOptionData(option.id, option.label, option.description);
            }

            _activeInteractionOptionsElement = ChatMessageElementFactory.CreateInteractionOptionsElement(
                optionData,
                string.Equals(request.selection_mode, "multiple", StringComparison.OrdinalIgnoreCase),
                OnClarificationOptionsSubmitted);

            AttachElementToMessageExtras(latestMessage, _activeInteractionOptionsElement.Root);
        }

        private void AttachProposalOptionsToLatestMessage(ProposalReadyMessage proposal)
        {
            if (proposal?.options == null || proposal.options.Length == 0)
                return;

            var latestMessage = GetLatestRenderedMessageElement();
            if (latestMessage == null)
                return;

            var optionData = new ChatInteractionOptionData[proposal.options.Length];
            for (int index = 0; index < proposal.options.Length; index++)
            {
                var option = proposal.options[index];
                optionData[index] = new ChatInteractionOptionData(option.id, option.label, option.description);
            }

            _activeInteractionOptionsElement = ChatMessageElementFactory.CreateInteractionOptionsElement(
                optionData,
                allowMultiple: false,
                OnProposalOptionsSubmitted);

            AttachElementToMessageExtras(latestMessage, _activeInteractionOptionsElement.Root);
        }

        private VisualElement GetLatestRenderedMessageElement()
        {
            for (int index = messageHistory.Count - 1; index >= 0; index--)
            {
                var message = messageHistory[index];
                if (messageUIElements.TryGetValue(message.messageId, out var element))
                    return element;
            }

            return null;
        }

        private static void AttachElementToMessageExtras(VisualElement messageElement, VisualElement child)
        {
            if (messageElement == null || child == null)
                return;

            var extras = messageElement.Q<VisualElement>("chat-message-extras");
            extras?.Add(child);
        }

        private void OnClarificationOptionsSubmitted(string[] selectedIds)
        {
            if (_pendingClarification == null)
                return;

            _clarificationReplySender(_pendingClarification.question_id, selectedIds ?? Array.Empty<string>(), string.Empty);
            _pendingClarification = null;
            DisableActiveInteractionOptions();
            UpdateInputPlaceholder();
        }

        private void OnProposalOptionsSubmitted(string[] selectedIds)
        {
            if (_pendingProposal == null)
                return;

            _proposalSelectSender(_pendingProposal.proposal_id, selectedIds ?? Array.Empty<string>(), string.Empty);
            _pendingProposal = null;
            DisableActiveInteractionOptions();
            UpdateInputPlaceholder();
        }

        private void DisableActiveInteractionOptions()
        {
            _activeInteractionOptionsElement?.SetInteractable(false);
            _activeInteractionOptionsElement = null;
        }

        private void UpdateInputPlaceholder()
        {
            var placeholder = DefaultInputPlaceholder;
            if (_pendingClarification != null)
                placeholder = "Choose a clarification option or type an alternative.";
            else if (_pendingProposal != null)
                placeholder = "Choose a proposal option or type a note.";

            if (_chatPanelView != null && _chatPanelView.IsReady)
                _chatPanelView.SetPlaceholder(placeholder);
        }

        public static string FormatJobLifecycle(JobLifecycleMessage job)
        {
            var message = job.payload?["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(message))
            {
                var label = string.Equals(job.job_kind, "experiment.run", StringComparison.OrdinalIgnoreCase)
                    ? "Experiment"
                    : "Job";
                return job.type switch
                {
                    "job.started" => $"{label} started: {message}",
                    "job.completed" => $"{label} completed: {message}",
                    "job.cancelled" => string.IsNullOrWhiteSpace(job.reason)
                        ? $"{label} cancelled: {message}"
                        : $"{label} cancelled: {message} - {job.reason}",
                    _ => string.IsNullOrWhiteSpace(job.reason)
                        ? $"{label} status: {message}"
                        : $"{label} status: {message}: {job.reason}"
                };
            }

            var shortJobId = string.IsNullOrWhiteSpace(job.job_id) || job.job_id.Length <= 8
                ? job.job_id
                : job.job_id.Substring(0, 8);
            return job.type switch
            {
                "job.started" => $"Job started: {job.job_kind} ({shortJobId})",
                "job.completed" => $"Job completed: {job.job_kind} ({shortJobId})",
                "job.cancelled" => string.IsNullOrWhiteSpace(job.reason)
                    ? $"Job cancelled: {job.job_kind} ({shortJobId})"
                    : $"Job cancelled: {job.job_kind} ({shortJobId}) - {job.reason}",
                _ => string.IsNullOrWhiteSpace(job.reason)
                    ? $"Job status: {job.job_kind} ({shortJobId}) - {job.status}"
                    : $"Job status: {job.job_kind} ({shortJobId}) - {job.status}: {job.reason}"
            };
        }

        private static string FormatDelegatedTaskResult(DelegatedTaskResultMessage result)
        {
            return string.IsNullOrWhiteSpace(result.reply) ? string.Empty : result.reply;
        }

        private List<LocalDelegatedTaskArtifact> PersistDelegatedTaskArtifacts(DelegatedTaskArtifactsSnapshotMessage snapshot)
        {
            var localArtifacts = new List<LocalDelegatedTaskArtifact>();
            if (snapshot.artifacts == null || snapshot.artifacts.Length == 0)
                return localArtifacts;

            var targetRoot = Path.Combine(Application.persistentDataPath, "DelegatedTaskArtifacts", snapshot.job_id);
            Directory.CreateDirectory(targetRoot);

            foreach (var artifact in snapshot.artifacts)
            {
                if (artifact == null || string.IsNullOrWhiteSpace(artifact.file_name) || string.IsNullOrWhiteSpace(artifact.content_base64))
                    continue;

                try
                {
                    var safeName = Path.GetFileName(artifact.file_name);
                    var localPath = Path.Combine(targetRoot, safeName);
                    var bytes = Convert.FromBase64String(artifact.content_base64);
                    File.WriteAllBytes(localPath, bytes);
                    localArtifacts.Add(new LocalDelegatedTaskArtifact
                    {
                        fileName = safeName,
                        kind = artifact.kind ?? "file",
                        label = string.IsNullOrWhiteSpace(artifact.label) ? Path.GetFileNameWithoutExtension(safeName) : artifact.label,
                        serverPath = artifact.server_path ?? string.Empty,
                        localPath = localPath,
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ChatUIManager] Failed to persist delegated artifact '{artifact.file_name}': {ex.Message}");
                }
            }

            return localArtifacts;
        }

        private void AttachDelegatedTaskArtifactsToMessage(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return;
            if (!_delegatedTaskMessageIdsByJobId.TryGetValue(jobId, out var messageId))
                return;
            if (!_delegatedTaskArtifactsByJobId.TryGetValue(jobId, out var artifacts) || artifacts == null || artifacts.Count == 0)
                return;

            if (messageUIElements.TryGetValue(messageId, out var element))
            {
                var extras = element.Q<VisualElement>("chat-message-extras");
                if (extras == null)
                    return;

                var existing = extras.Q<VisualElement>("DelegatedTaskAttachments");
                existing?.RemoveFromHierarchy();

                var attachmentItems = new List<ChatAttachmentItem>(artifacts.Count);
                foreach (var artifact in artifacts)
                {
                    attachmentItems.Add(new ChatAttachmentItem(
                        artifact.fileName,
                        artifact.kind,
                        artifact.label,
                        artifact.localPath,
                        artifact.serverPath));
                }

                var attachmentList = ChatMessageElementFactory.CreateAttachmentListElement(
                    attachmentItems,
                    OpenDelegatedTaskArtifact);
                attachmentList.name = "DelegatedTaskAttachments";
                extras.Add(attachmentList);
            }
        }

        private void OpenDelegatedTaskArtifact(ChatAttachmentItem artifact)
        {
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.LocalPath))
                return;
            if (!File.Exists(artifact.LocalPath))
            {
                AddSystemMessage($"⚠️ Attachment not found: {artifact.FileName}");
                return;
            }

            try
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                System.Diagnostics.Process.Start("open", $"\"{artifact.LocalPath}\"");
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = artifact.LocalPath,
                    UseShellExecute = true,
                });
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                System.Diagnostics.Process.Start("xdg-open", $"\"{artifact.LocalPath}\"");
#else
                var normalizedPath = artifact.LocalPath.Replace("\\", "/");
                Application.OpenURL($"file://{normalizedPath}");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChatUIManager] Failed to open delegated artifact '{artifact.LocalPath}': {ex.Message}");
                AddSystemMessage($"⚠️ Failed to open attachment: {artifact.FileName}");
            }
        }

        private void StartAutoScroll()
        {
            if (autoScrollCoroutine != null)
                StopCoroutine(autoScrollCoroutine);

            autoScrollCoroutine = StartCoroutine(ScrollToBottom());
        }

        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();

            if (_chatPanelView != null && _chatPanelView.IsReady)
                _chatPanelView.ScrollToBottom();
        }

        public void ToggleChatWindow()
        {
            if (_chatPanelView != null && _chatPanelView.IsReady)
                _chatPanelView.SetPanelVisible(!_chatPanelView.IsPanelVisible);
        }

        public void SetChatContentVisible(bool visible)
        {
            if (_chatPanelView != null && _chatPanelView.IsReady)
                _chatPanelView.SetChatContentVisible(visible);
        }

        public void ClearChatHistory()
        {
            messageHistory.Clear();
            _pendingClarification = null;
            _pendingProposal = null;
            _activeInteractionOptionsElement = null;
            UpdateInputPlaceholder();

            foreach (var pair in messageUIElements)
                pair.Value?.RemoveFromHierarchy();
            messageUIElements.Clear();

            AddSystemMessage("💬 Chat history cleared...");
        }

        public List<ChatMessage> GetMessageHistory()
        {
            return new List<ChatMessage>(messageHistory);
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

        private void OnToggleViewClicked()
        {
            var cameraControl = ServiceLocator.Get<UserMainCameraControl>();
            if (cameraControl != null)
            {
                cameraControl.ToggleCameraMode();
                string modeName = cameraControl.GetCurrentMode() == UserMainCameraControl.CameraMode.FirstPerson
                    ? "First Person"
                    : "God View";
                AddSystemMessage($"📷 Switched to {modeName} Mode");
            }
            else
            {
                Debug.LogError("[ChatUIManager] UserMainCameraControl not found!");
            }
        }

        private void UpdateEditModeButtonBG()
        {
            if (editModeButton == null)
                return;

            _editModeButtonImage ??= editModeButton.GetComponent<MaterialSymbol>();
            if (_editModeButtonImage == null)
                return;

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
                return;

            runtimeEditModeController = ResolveRuntimeEditModeController(createIfMissing: false);
            runtimeTransformHandleBridge = ResolveRuntimeTransformHandleBridge(createIfMissing: false);

            bool shouldShow = runtimeEditModeController != null
                              && runtimeEditModeController.IsEditModeEnabled
                              && !string.IsNullOrWhiteSpace(runtimeEditModeController.SelectedObjectId)
                              && runtimeTransformHandleBridge != null
                              && runtimeTransformHandleBridge.SupportsCurrentSelection;

            if (transformGizmoUI.gameObject.activeSelf != shouldShow)
                transformGizmoUI.gameObject.SetActive(shouldShow);

            if (shouldShow)
                transformGizmoUI.SetMode(runtimeTransformHandleBridge.CurrentHandleType);
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
                return runtimeEditModeController;

            runtimeEditModeController = ServiceLocator.Get<RuntimeEditModeController>() ?? FindFirstObjectByType<RuntimeEditModeController>();
            if (runtimeEditModeController != null || !createIfMissing)
                return runtimeEditModeController;

            var runtime = ServiceLocator.Get<VsensAgent.SceneApi.V2.AvatarRuntimeManager>() ??
                          FindFirstObjectByType<VsensAgent.SceneApi.V2.AvatarRuntimeManager>();
            GameObject host = runtime != null ? runtime.gameObject : new GameObject("RuntimeEditModeController");
            runtimeEditModeController = host.GetComponent<RuntimeEditModeController>() ?? host.AddComponent<RuntimeEditModeController>();
            if (runtime != null)
                runtimeEditModeController.Configure(runtime);
            return runtimeEditModeController;
        }

        private RuntimeTransformHandleBridge ResolveRuntimeTransformHandleBridge(bool createIfMissing)
        {
            if (runtimeTransformHandleBridge != null)
                return runtimeTransformHandleBridge;

            var controller = ResolveRuntimeEditModeController(createIfMissing);
            if (controller == null)
                return null;

            runtimeTransformHandleBridge = controller.GetComponent<RuntimeTransformHandleBridge>();
            if (runtimeTransformHandleBridge == null && createIfMissing)
                runtimeTransformHandleBridge = controller.gameObject.AddComponent<RuntimeTransformHandleBridge>();
            return runtimeTransformHandleBridge;
        }
    }

}
