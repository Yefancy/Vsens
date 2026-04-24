using NUnit.Framework;
using UnityEngine.UIElements;
using VsensAgent.UI;

namespace VsensAgent.Tests.Editor.UI
{
    public class ChatPanelViewTests
    {
        [Test]
        public void ChatPanelView_BindsNamedElementsAndUpdatesCommonState()
        {
            var root = BuildChatPanelTree();

            var view = new ChatPanelView(root);
            view.SetPlaceholder("Choose a proposal option or type a note.");
            view.SetSendButtonText("Stop", true);
            view.SetChatContentVisible(false);

            Assert.That(view.MessagesView, Is.Not.Null);
            Assert.That(view.InputField, Is.Not.Null);
            Assert.That(view.SendButton, Is.Not.Null);
            Assert.That(view.InputField.textEdition.placeholder, Is.EqualTo("Choose a proposal option or type a note."));
            Assert.That(view.SendButton.text, Is.EqualTo("Stop"));
            Assert.That(view.TopSection.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.BottomSection.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        private static VisualElement BuildChatPanelTree()
        {
            var root = new VisualElement();
            var panel = new VisualElement { name = "chat-panel" };
            var top = new VisualElement { name = "top" };
            var bottom = new VisualElement { name = "bottom" };
            var scrollView = new ScrollView { name = "ScrollView" };
            var textField = new TextField { name = "TextField", multiline = true };
            var sendButton = new Button { name = "SendButton", text = "OK" };

            top.Add(scrollView);
            bottom.Add(textField);
            bottom.Add(sendButton);
            panel.Add(top);
            panel.Add(bottom);
            root.Add(panel);
            return root;
        }
    }
}
