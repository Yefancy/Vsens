using System;

namespace VsensAgent.UI
{
    public sealed class ChatAttachmentItem
    {
        public string FileName { get; }
        public string Kind { get; }
        public string Label { get; }
        public string LocalPath { get; }
        public string ServerPath { get; }

        public ChatAttachmentItem(string fileName, string kind, string label, string localPath = "", string serverPath = "")
        {
            FileName = fileName ?? string.Empty;
            Kind = kind ?? "file";
            Label = string.IsNullOrWhiteSpace(label) ? FileName : label;
            LocalPath = localPath ?? string.Empty;
            ServerPath = serverPath ?? string.Empty;
        }
    }
}
