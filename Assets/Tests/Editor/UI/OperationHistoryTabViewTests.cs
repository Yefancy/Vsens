using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VsensAgent.Core;
using VsensAgent.SceneHistory;
using VsensAgent.UI;

namespace VsensAgent.Tests.Editor.UI
{
    public class OperationHistoryTabViewTests
    {
        [Test]
        public void OperationHistoryTabView_CreatesActionsTabAndRendersHistoryRows()
        {
            ServiceLocator.Clear();
            var root = new GameObject("ChatWindow", typeof(RectTransform));
            var messageScrollArea = new GameObject("MessageScrollArea", typeof(RectTransform), typeof(ScrollRect));
            var inputArea = new GameObject("InputArea", typeof(RectTransform));
            var inputFieldGo = new GameObject("TextInputField", typeof(RectTransform), typeof(TMP_InputField));
            GameObject target = null;

            try
            {
                messageScrollArea.transform.SetParent(root.transform, false);
                inputArea.transform.SetParent(root.transform, false);
                inputFieldGo.transform.SetParent(inputArea.transform, false);

                var chatUi = root.AddComponent<ChatUIManager>();
                chatUi.messageScrollRect = messageScrollArea.GetComponent<ScrollRect>();
                chatUi.textInputField = inputFieldGo.GetComponent<TMP_InputField>();

                var history = root.AddComponent<SceneActionHistory>();
                var view = root.AddComponent<OperationHistoryTabView>();
                view.Initialize();
                history.Clear();

                Assert.That(root.transform.Find("TabBar"), Is.Not.Null, "TabBar was not created.");
                Assert.That(root.transform.Find("ActionScrollArea"), Is.Not.Null, "ActionScrollArea was not created.");

                target = new GameObject("Movable");
                target.transform.position = Vector3.zero;
                var before = SceneTransformSnapshot.Capture(target.name, target.transform);
                target.transform.position = new Vector3(1f, 0f, 0f);
                var after = SceneTransformSnapshot.Capture(target.name, target.transform);
                Assert.That(history.TryRecordTransformChange("user", "set_transform", target.name, before, after), Is.True);

                view.ShowActions();

                var row = root.transform.Find("ActionScrollArea/Viewport/ActionContainer/ActionRow");
                Assert.That(row, Is.Not.Null, $"Action row was not created. Operation count: {history.OperationLog.Count}");
                var text = row.GetComponentInChildren<TextMeshProUGUI>();
                Assert.That(text, Is.Not.Null, "Action row text was not created.");
                Assert.That(text.text, Does.Contain("user set_transform Movable"));
                Assert.That(messageScrollArea.activeSelf, Is.False);
                Assert.That(inputArea.activeSelf, Is.False);
            }
            finally
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }

                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }
    }
}
