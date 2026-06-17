using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;
using VsensAgent.Data;
using VsensAgent.UI;

namespace VsensAgent.Tests.Editor.UI
{
    public class ChatMessageElementFactoryTests
    {
        [Test]
        public void CreateMessageElement_RendersAgentReplayButtonAndExtrasContainer()
        {
            var message = new ChatMessage("reply", ChatMessage.MessageType.Agent, "reply.mp3");
            var replayed = false;

            var element = ChatMessageElementFactory.CreateMessageElement(
                message,
                _ => replayed = true);

            var replayButton = element.Q<Button>("chat-message-audio-button");
            Assert.That(replayButton, Is.Not.Null);

            Click(replayButton);

            Assert.That(replayed, Is.True);
            Assert.That(element.Q<VisualElement>("chat-message-extras"), Is.Not.Null);
        }

        [Test]
        public void CreateInteractionOptionsElement_SubmitsSelectedIds()
        {
            var submitted = Array.Empty<string>();
            var optionsElement = ChatMessageElementFactory.CreateInteractionOptionsElement(
                new[]
                {
                    new ChatInteractionOptionData("opt_a", "Option A", "alpha"),
                    new ChatInteractionOptionData("opt_b", "Option B", "beta"),
                },
                allowMultiple: true,
                selectedIds => submitted = selectedIds);

            var optionA = optionsElement.Root.Q<Button>("chat-option-opt_a");
            var submit = optionsElement.Root.Q<Button>("chat-options-submit");

            Assert.That(optionA, Is.Not.Null);
            Assert.That(submit, Is.Not.Null);

            Click(optionA);
            Click(submit);

            Assert.That(submitted, Is.EqualTo(new[] { "opt_a" }));
        }

        [Test]
        public void CreateInteractionOptionsElement_ConfiguresLongOptionTextToWrapWithinButton()
        {
            var optionsElement = ChatMessageElementFactory.CreateInteractionOptionsElement(
                new[]
                {
                    new ChatInteractionOptionData(
                        "long_option",
                        "Analyze the latest recording and compare whether the cooking phase or the dish washing phase better matches the current HAR collection target",
                        "Use this when the user wants a detailed comparison with metrics, plots, and a recommendation.")
                },
                allowMultiple: true,
                _ => { });

            var option = optionsElement.Root.Q<Button>("chat-option-long_option");
            var label = option.Q<Label>(className: "chat-option-label");
            var description = option.Q<Label>(className: "chat-option-description");

            Assert.That(option.style.flexDirection.value, Is.EqualTo(FlexDirection.Column));
            Assert.That(option.style.alignItems.value, Is.EqualTo(Align.Stretch));
            Assert.That(option.style.width.value.value, Is.EqualTo(100f));
            Assert.That(option.style.width.value.unit, Is.EqualTo(LengthUnit.Percent));
            Assert.That(option.style.flexShrink.value, Is.EqualTo(1f));
            Assert.That(label.style.whiteSpace.value, Is.EqualTo(WhiteSpace.Normal));
            Assert.That(label.style.flexShrink.value, Is.EqualTo(1f));
            Assert.That(description.style.whiteSpace.value, Is.EqualTo(WhiteSpace.Normal));
            Assert.That(description.style.flexShrink.value, Is.EqualTo(1f));
        }

        [Test]
        public void CreateAttachmentListElement_RendersHeaderAndButtons()
        {
            var opened = string.Empty;
            var element = ChatMessageElementFactory.CreateAttachmentListElement(
                new[]
                {
                    new ChatAttachmentItem("diagram.png", "image", "diagram"),
                },
                attachment => opened = attachment.FileName);

            Assert.That(element.Q<Label>("chat-attachments-header"), Is.Not.Null);

            var button = element.Q<Button>("chat-attachment-diagram.png");
            Assert.That(button, Is.Not.Null);

            Click(button);

            Assert.That(opened, Is.EqualTo("diagram.png"));
        }

        [Test]
        public void CreateAttachmentListElement_RendersImagePreviewForImageAttachment()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
            try
            {
                File.WriteAllBytes(tempFile, Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO7Z0X8AAAAASUVORK5CYII="));

                var element = ChatMessageElementFactory.CreateAttachmentListElement(
                    new[]
                    {
                        new ChatAttachmentItem("preview.png", "image", "preview", tempFile),
                    },
                    _ => { });

                var preview = element.Q<Image>(className: "chat-attachment-preview");
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.image, Is.Not.Null);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        private static void Click(Button button)
        {
            if (button.userData is Action invoke)
            {
                invoke();
                return;
            }

            using var clickEvent = ClickEvent.GetPooled();
            clickEvent.target = button;
            button.SendEvent(clickEvent);
        }
    }
}
