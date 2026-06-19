using NUnit.Framework;
using UnityEngine;

namespace VsensAgent.Tests.Editor.Core
{
    public class UserMainCameraControlTests
    {
        [Test]
        public void IsDescendKey_IncludesShiftKeysAndC()
        {
            Assert.That(UserMainCameraControl.IsDescendKey(KeyCode.C), Is.True);
            Assert.That(UserMainCameraControl.IsDescendKey(KeyCode.LeftShift), Is.True);
            Assert.That(UserMainCameraControl.IsDescendKey(KeyCode.RightShift), Is.True);
        }

        [Test]
        public void BuildFirstPersonMovementDirection_DescendMovesDown()
        {
            var direction = UserMainCameraControl.BuildFirstPersonMovementDirection(
                Vector3.forward,
                Vector3.right,
                moveForward: false,
                moveBackward: false,
                moveLeft: false,
                moveRight: false,
                ascend: false,
                descend: true);

            Assert.That(direction, Is.EqualTo(Vector3.down));
        }

        [Test]
        public void BuildGodViewMovement_DescendMovesTargetDown()
        {
            var movement = UserMainCameraControl.BuildGodViewMovement(
                yaw: 0f,
                moveForward: false,
                moveBackward: false,
                moveLeft: false,
                moveRight: false,
                ascend: false,
                descend: true);

            Assert.That(movement, Is.EqualTo(Vector3.down));
        }
    }
}
