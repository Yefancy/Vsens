using System;
using UnityEngine;

namespace VsensAgent.SceneHistory
{
    [Serializable]
    public sealed class SceneTransformSnapshot
    {
        public string objectId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
        public string parentName;

        public static SceneTransformSnapshot Capture(string objectId, Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            return new SceneTransformSnapshot
            {
                objectId = string.IsNullOrWhiteSpace(objectId) ? transform.name : objectId,
                position = transform.position,
                rotation = transform.rotation,
                localScale = transform.localScale,
                parentName = transform.parent != null ? transform.parent.name : string.Empty
            };
        }

        public bool ApproximatelyEquals(SceneTransformSnapshot other)
        {
            if (other == null)
            {
                return false;
            }

            return Vector3.SqrMagnitude(position - other.position) < 0.000001f &&
                   Quaternion.Angle(rotation, other.rotation) < 0.001f &&
                   Vector3.SqrMagnitude(localScale - other.localScale) < 0.000001f &&
                   string.Equals(parentName ?? string.Empty, other.parentName ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
