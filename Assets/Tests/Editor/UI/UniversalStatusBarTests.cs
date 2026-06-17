using NUnit.Framework;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.Network.Protocol;
using VsensAgent.UI;

namespace VsensAgent.Tests.Editor.UI
{
    public class UniversalStatusBarTests
    {
        [Test]
        public void UniversalStatusBar_UsesHoverHintOverTransientAndAgentStatus()
        {
            ServiceLocator.Clear();
            var root = new GameObject("StatusBarRoot");
            var textObject = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            var button = new GameObject("HintButton");

            try
            {
                textObject.transform.SetParent(root.transform, false);
                var text = textObject.GetComponent<TextMeshProUGUI>();
                var statusBar = root.AddComponent<UniversalStatusBar>();
                var serializedStatusBar = new SerializedObject(statusBar);
                serializedStatusBar.FindProperty("statusText").objectReferenceValue = text;
                serializedStatusBar.ApplyModifiedPropertiesWithoutUndo();

                statusBar.SetAgentStatus("thinking", "planning");
                Assert.That(text.text, Is.EqualTo("Agent: Thinking - planning"));

                statusBar.PushMessage("Saved", 2f);
                Assert.That(text.text, Is.EqualTo("Saved"));

                var trigger = button.AddComponent<StatusHintTrigger>();
                trigger.SetHint("Undo the latest scene action");

                trigger.OnPointerEnter(null);
                Assert.That(text.text, Is.EqualTo("Undo the latest scene action"));

                trigger.OnPointerExit(null);
                Assert.That(text.text, Is.EqualTo("Saved"));
            }
            finally
            {
                Object.DestroyImmediate(button);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void ChatUIManager_FormatJobLifecycle_UsesPayloadMessageWhenAvailable()
        {
            var message = ChatUIManager.FormatJobLifecycle(new JobLifecycleMessage
            {
                type = "job.status",
                job_id = "926608a1d89142218a5800f1a9e27816",
                job_kind = "experiment.run",
                status = "running",
                payload = JObject.FromObject(new
                {
                    message = "Recording phase cooking repeat 2 for 15s"
                })
            });

            Assert.That(message, Is.EqualTo("Experiment status: Recording phase cooking repeat 2 for 15s"));
            Assert.That(message, Does.Not.Contain("926608a1"));
        }

        [Test]
        public void ChatUIManager_FormatJobLifecycle_TruncatesOpaqueJobIdFallback()
        {
            var message = ChatUIManager.FormatJobLifecycle(new JobLifecycleMessage
            {
                type = "job.status",
                job_id = "926608a1d89142218a5800f1a9e27816",
                job_kind = "experiment.run",
                status = "running"
            });

            Assert.That(message, Is.EqualTo("Job status: experiment.run (926608a1) - running"));
        }
    }
}
