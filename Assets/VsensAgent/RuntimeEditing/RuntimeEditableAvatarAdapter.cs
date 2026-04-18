using UnityEngine;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.RuntimeEditing
{
    public sealed class RuntimeEditableAvatarAdapter : IRuntimeEditableObject
    {
        private readonly AvatarRuntimeManager _runtimeManager;
        private readonly string _avatarId;

        public RuntimeEditableAvatarAdapter(AvatarRuntimeManager runtimeManager, string avatarId = "avatar_main")
        {
            _runtimeManager = runtimeManager;
            _avatarId = string.IsNullOrWhiteSpace(avatarId) ? "avatar_main" : avatarId;
        }

        public string ObjectId => _avatarId;

        public bool Matches(GameObject gameObject)
        {
            var avatar = _runtimeManager != null ? _runtimeManager.GetManagedAvatarObject() : null;
            if (avatar == null || gameObject == null)
            {
                return false;
            }

            return gameObject == avatar || gameObject.transform.IsChildOf(avatar.transform);
        }

        public Transform GetTransform()
        {
            return _runtimeManager != null ? _runtimeManager.GetManagedAvatarObject()?.transform : null;
        }

        public Vector3 GetForward()
        {
            var transform = GetTransform();
            if (transform == null)
            {
                return Vector3.forward;
            }

            return AvatarRuntimeManager.GetLogicalForward(transform.rotation);
        }

        public bool TryMoveToGroundPoint(Vector3 worldPoint, out string error)
        {
            if (_runtimeManager == null)
            {
                error = "Avatar runtime manager is not available.";
                return false;
            }

            bool ok = _runtimeManager.TrySetManualAvatarPose(_avatarId, worldPoint, null, out error);
            return ok;
        }

        public bool TryRotateYaw(float yawDegrees, out string error)
        {
            if (_runtimeManager == null)
            {
                error = "Avatar runtime manager is not available.";
                return false;
            }

            bool ok = _runtimeManager.TrySetManualAvatarPose(_avatarId, null, yawDegrees, out error);
            return ok;
        }

        public bool CommitTransformMutation(out string error)
        {
            if (_runtimeManager == null)
            {
                error = "Avatar runtime manager is not available.";
                return false;
            }

            var transform = GetTransform();
            if (transform == null)
            {
                error = "Avatar transform is not available.";
                return false;
            }

            var logicalYaw = AvatarRuntimeManager.GetLogicalRotationEuler(transform.rotation).y;
            return _runtimeManager.TrySetManualAvatarPose(_avatarId, transform.position, logicalYaw, out error);
        }
    }
}
