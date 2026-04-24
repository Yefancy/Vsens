using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using VsensAgent.Data;
using UIEButton = UnityEngine.UIElements.Button;

namespace VsensAgent.UI
{
    public static class ChatMessageElementFactory
    {
        public static VisualElement CreateMessageElement(ChatMessage message, Action<ChatMessage> onReplay, ViewsCollection markdownViews = null)
        {
            string roleClass = message.type.ToString().ToLowerInvariant();
            var row = new VisualElement { name = $"chat-message-{message.messageId}" };
            row.AddToClassList("chat-message-row");
            row.AddToClassList($"chat-message-row--{roleClass}");

            var bubble = new VisualElement();
            bubble.AddToClassList("chat-message-bubble");
            bubble.AddToClassList($"chat-message-bubble--{roleClass}");
            row.Add(bubble);

            var meta = new VisualElement { name = "chat-message-meta" };
            meta.AddToClassList("chat-message-meta");

            var role = new Label(GetRoleLabel(message.type)) { name = "chat-message-role" };
            role.AddToClassList("chat-message-role");
            role.AddToClassList($"chat-message-role--{roleClass}");
            meta.Add(role);
            bubble.Add(meta);

            bubble.Add(CreateContentElement(message, markdownViews));

            if (message.type == ChatMessage.MessageType.Agent && message.HasAudio())
            {
                var controls = new VisualElement();
                controls.AddToClassList("chat-message-controls");

                var replayButton = new UIEButton(() => onReplay?.Invoke(message))
                {
                    name = "chat-message-audio-button",
                    text = "Replay"
                };
                replayButton.AddToClassList("chat-message-audio-button");
                replayButton.userData = (Action)(() => onReplay?.Invoke(message));
                replayButton.RegisterCallback<ClickEvent>(_ => onReplay?.Invoke(message));
                controls.Add(replayButton);
                bubble.Add(controls);
            }

            var extras = new VisualElement { name = "chat-message-extras" };
            extras.AddToClassList("chat-message-extras");
            bubble.Add(extras);
            return row;
        }

        public static ChatInteractionOptionsElement CreateInteractionOptionsElement(
            IReadOnlyList<ChatInteractionOptionData> options,
            bool allowMultiple,
            Action<string[]> onSubmit)
        {
            return new ChatInteractionOptionsElement(options, allowMultiple, onSubmit);
        }

        public static VisualElement CreateAttachmentListElement(
            IReadOnlyList<ChatAttachmentItem> attachments,
            Action<ChatAttachmentItem> onOpen)
        {
            var root = new VisualElement { name = "chat-attachments" };
            root.AddToClassList("chat-attachments");

            var header = new Label("Attachments") { name = "chat-attachments-header" };
            header.AddToClassList("chat-attachments-header");
            root.Add(header);

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    var item = new VisualElement { name = $"chat-attachment-item-{attachment.FileName}" };
                    item.AddToClassList("chat-attachment-item");

                    var button = new UIEButton(() => onOpen?.Invoke(attachment))
                    {
                        name = $"chat-attachment-{attachment.FileName}",
                        text = $"[{attachment.Kind}] {attachment.FileName}"
                    };
                    button.AddToClassList("chat-attachment-button");
                    button.userData = (Action)(() => onOpen?.Invoke(attachment));
                    button.RegisterCallback<ClickEvent>(_ => onOpen?.Invoke(attachment));
                    item.Add(button);

                    var preview = CreateAttachmentPreviewElement(attachment);
                    if (preview != null)
                        item.Add(preview);

                    root.Add(item);
                }
            }

            return root;
        }

        private static VisualElement CreateContentElement(ChatMessage message, ViewsCollection markdownViews)
        {
            if (message.type == ChatMessage.MessageType.Agent && markdownViews != null)
            {
                var markdownContainer = new VisualElement { name = "chat-message-content" };
                markdownContainer.AddToClassList("chat-message-content");
                markdownContainer.AddToClassList("chat-message-content--markdown");

                var renderer = new MarkdownRenderer
                {
                    name = "chat-message-markdown-renderer"
                };
                renderer.AddToClassList("chat-markdown-renderer");
                renderer.Initialize(renderer, markdownViews);
                renderer.Text = message.content ?? string.Empty;
                markdownContainer.Add(renderer);
                return markdownContainer;
            }

            var content = new Label(message.content ?? string.Empty) { name = "chat-message-content" };
            content.AddToClassList("chat-message-content");
            return content;
        }

        private static string GetRoleLabel(ChatMessage.MessageType type)
        {
            return type switch
            {
                ChatMessage.MessageType.User => "You",
                ChatMessage.MessageType.Agent => "Agent",
                ChatMessage.MessageType.System => "System",
                _ => "Message"
            };
        }

        private static VisualElement CreateAttachmentPreviewElement(ChatAttachmentItem attachment)
        {
            if (!IsPreviewableImageAttachment(attachment))
                return null;

            try
            {
                var bytes = File.ReadAllBytes(attachment.LocalPath);
                if (bytes.Length == 0)
                    return null;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes, markNonReadable: true))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    return null;
                }

                var preview = new Image
                {
                    image = texture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                preview.AddToClassList("chat-attachment-preview");
                preview.tooltip = attachment.FileName;
                return preview;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChatMessageElementFactory] Failed to create attachment preview for '{attachment?.LocalPath}': {ex.Message}");
                return null;
            }
        }

        private static bool IsPreviewableImageAttachment(ChatAttachmentItem attachment)
        {
            if (attachment == null || string.IsNullOrWhiteSpace(attachment.LocalPath) || !File.Exists(attachment.LocalPath))
                return false;

            if (string.Equals(attachment.Kind, "image", StringComparison.OrdinalIgnoreCase))
                return true;

            var extension = Path.GetExtension(attachment.LocalPath);
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tga", StringComparison.OrdinalIgnoreCase);
        }
    }
}
