using UnityEngine;
using UnityEngine.UIElements;
using UIEButton = UnityEngine.UIElements.Button;

namespace VsensAgent.UI
{
    public sealed class ChatPanelView
    {
        public VisualElement Root { get; }
        public VisualElement Panel { get; }
        public VisualElement TopSection { get; }
        public VisualElement BottomSection { get; }
        public ScrollView MessagesView { get; }
        public TextField InputField { get; }
        public UIEButton SendButton { get; }

        public bool IsReady => Panel != null && TopSection != null && BottomSection != null && MessagesView != null && InputField != null && SendButton != null;
        public bool IsPanelVisible => Panel != null && Panel.resolvedStyle.display != DisplayStyle.None;
        public bool IsInputFocused => InputField != null && InputField.focusController?.focusedElement == InputField;

        public ChatPanelView(VisualElement root)
        {
            Root = root;
            Panel = root.Q<VisualElement>("chat-panel");
            TopSection = root.Q<VisualElement>("top");
            BottomSection = root.Q<VisualElement>("bottom");
            MessagesView = root.Q<ScrollView>("ScrollView");
            InputField = root.Q<TextField>("TextField");
            SendButton = root.Q<UIEButton>("SendButton");
        }

        public void SetPlaceholder(string text)
        {
            if (InputField?.textEdition != null)
                InputField.textEdition.placeholder = text ?? string.Empty;
        }

        public void SetSendButtonText(string text, bool isStopState = false)
        {
            if (SendButton == null)
                return;

            SendButton.text = text ?? string.Empty;
            SendButton.EnableInClassList("chat-send-button--stop", isStopState);
        }

        public void FocusInput()
        {
            if (InputField == null)
                return;

            InputField.Focus();
            string value = InputField.value ?? string.Empty;
            InputField.SelectRange(value.Length, value.Length);
        }

        public void BlurInput()
        {
            InputField?.Blur();
        }

        public void RegisterInputKeyDown(EventCallback<KeyDownEvent> callback, TrickleDown trickleDown = TrickleDown.NoTrickleDown)
        {
            InputField?.RegisterCallback(callback, trickleDown);
        }

        public void UnregisterInputKeyDown(EventCallback<KeyDownEvent> callback, TrickleDown trickleDown = TrickleDown.NoTrickleDown)
        {
            InputField?.UnregisterCallback(callback, trickleDown);
        }

        public void ClearMessages()
        {
            MessagesView?.contentContainer.Clear();
        }

        public void AddMessageElement(VisualElement element)
        {
            MessagesView?.contentContainer.Add(element);
        }

        public void ScrollToBottom()
        {
            if (MessagesView == null)
                return;

            MessagesView.scrollOffset = new Vector2(0f, float.MaxValue);
            MessagesView.MarkDirtyRepaint();
        }

        public void SetPanelVisible(bool visible)
        {
            if (Panel != null)
                Panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetChatContentVisible(bool visible)
        {
            if (TopSection != null)
                TopSection.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (BottomSection != null)
                BottomSection.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
