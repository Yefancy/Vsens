using Sensor;
using UnityEngine;

namespace VsensAgent.RuntimeEditing
{
    public sealed class RuntimeEditableSensorAdapter : IRuntimeEditableObject
    {
        private readonly VirtualSensor _sensor;

        public RuntimeEditableSensorAdapter(VirtualSensor sensor)
        {
            _sensor = sensor;
        }

        public string ObjectId => _sensor != null ? _sensor.name : string.Empty;

        public bool Matches(GameObject gameObject)
        {
            if (_sensor == null || gameObject == null)
            {
                return false;
            }

            return gameObject == _sensor.gameObject || gameObject.transform.IsChildOf(_sensor.transform);
        }

        public Transform GetTransform()
        {
            return _sensor != null ? _sensor.transform : null;
        }

        public Vector3 GetForward()
        {
            return _sensor != null ? _sensor.transform.forward : Vector3.forward;
        }

        public bool TryMoveToGroundPoint(Vector3 worldPoint, out string error)
        {
            if (_sensor == null)
            {
                error = "Sensor is not available.";
                return false;
            }

            _sensor.transform.position = worldPoint;
            error = null;
            return true;
        }

        public bool TryRotateYaw(float yawDegrees, out string error)
        {
            if (_sensor == null)
            {
                error = "Sensor is not available.";
                return false;
            }

            _sensor.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            error = null;
            return true;
        }

        public bool CommitTransformMutation(out string error)
        {
            error = null;
            return _sensor != null;
        }
    }
}
