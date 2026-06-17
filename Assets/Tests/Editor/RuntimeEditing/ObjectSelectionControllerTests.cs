using NUnit.Framework;
using UnityEngine;
using VsensAgent.Network.Protocol;
using VsensAgent.RuntimeEditing;

namespace VsensAgent.Tests.Editor.RuntimeEditing
{
    public class ObjectSelectionControllerTests
    {
        [Test]
        public void TrySelectFromRay_SelectsObjectDescriberAndBuildsReply()
        {
            var controllerObject = new GameObject("ObjectSelectionController");
            var cameraObject = new GameObject("SelectionCamera");
            var stoveObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ObjectSelectionReplyRequest reply = null;

            try
            {
                cameraObject.transform.position = Vector3.zero;
                cameraObject.transform.rotation = Quaternion.identity;
                var camera = cameraObject.AddComponent<Camera>();

                stoveObject.name = "Stove";
                stoveObject.transform.position = new Vector3(0f, 0f, 5f);
                stoveObject.AddComponent<ObjectDescriber>();

                var controller = controllerObject.AddComponent<ObjectSelectionController>();
                controller.Configure(camera, payload => reply = payload);
                controller.BeginSelection(new ObjectSelectionRequestMessage
                {
                    selection_id = "sel_stove",
                    prompt = "Select the stove",
                    mode = "object",
                    candidate_aliases = new[] { "Stove" },
                    allow_cancel = true,
                });

                Assert.That(controller.IsSelecting, Is.True);
                Assert.That(controller.TrySelectFromRay(new Ray(Vector3.zero, Vector3.forward)), Is.True);

                Assert.That(reply, Is.Not.Null);
                Assert.That(reply.type, Is.EqualTo("object_selection.reply"));
                Assert.That(reply.selection_id, Is.EqualTo("sel_stove"));
                Assert.That(reply.selected_object_id, Is.EqualTo("Stove"));
                Assert.That(reply.selected_alias, Is.EqualTo("Stove"));
                Assert.That(reply.cancelled, Is.False);
                Assert.That(controller.HighlightedObject, Is.EqualTo(stoveObject));
                Assert.That(controller.IsSelecting, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(stoveObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void CancelSelection_SendsCancelledReplyWhenAllowed()
        {
            var controllerObject = new GameObject("ObjectSelectionController");
            ObjectSelectionReplyRequest reply = null;

            try
            {
                var controller = controllerObject.AddComponent<ObjectSelectionController>();
                controller.Configure(null, payload => reply = payload);
                controller.BeginSelection(new ObjectSelectionRequestMessage
                {
                    selection_id = "sel_cancel",
                    prompt = "Select an object",
                    mode = "object",
                    allow_cancel = true,
                });

                Assert.That(controller.CancelSelection(), Is.True);
                Assert.That(reply, Is.Not.Null);
                Assert.That(reply.selection_id, Is.EqualTo("sel_cancel"));
                Assert.That(reply.cancelled, Is.True);
                Assert.That(controller.IsSelecting, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
            }
        }
    }
}

