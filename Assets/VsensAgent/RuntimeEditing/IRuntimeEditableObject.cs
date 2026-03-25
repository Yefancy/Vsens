using UnityEngine;

namespace VsensAgent.RuntimeEditing
{
    public interface IRuntimeEditableObject
    {
        string ObjectId { get; }
        bool Matches(GameObject gameObject);
        Transform GetTransform();
        bool TryMoveToGroundPoint(Vector3 worldPoint, out string error);
        bool TryRotateYaw(float yawDegrees, out string error);
    }
}
