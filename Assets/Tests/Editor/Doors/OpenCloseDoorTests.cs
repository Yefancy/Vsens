using NUnit.Framework;
using SojaExiles;
using UnityEngine;

namespace VsensAgent.Tests.Editor.Doors
{
    public class OpenCloseDoorTests
    {
        [Test]
        public void ToggleDoor_ChangesDoorStateWithoutPlayerReference()
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

                door.ToggleDoor(true);

                Assert.That(door.open, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
