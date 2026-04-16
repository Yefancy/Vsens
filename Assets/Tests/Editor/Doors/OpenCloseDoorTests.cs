using NUnit.Framework;
using SojaExiles;
using UnityEngine;
using UnityEngine.TestTools;

namespace VsensAgent.Tests.Editor.Doors
{
    public class OpenCloseDoorTests
    {
        [Test]
        public void OnMouseDown_TogglesDoorWithoutPlayerReference()
        {
            var go = new GameObject("Door");
            try
            {
                go.AddComponent<BoxCollider>();
                var animator = go.AddComponent<Animator>();
                var door = go.AddComponent<opencloseDoor>();
                door.openandclose = animator;
                door.open = false;
                door.Player = null;

                LogAssert.Expect(LogType.Assert, "Assertion failed on expression: 'ShouldRunBehaviour()'");
                go.SendMessage("OnMouseDown", SendMessageOptions.DontRequireReceiver);

                Assert.That(door.open, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
