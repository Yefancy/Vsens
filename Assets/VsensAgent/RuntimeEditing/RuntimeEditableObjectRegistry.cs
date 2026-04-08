using System.Collections.Generic;
using Sensor;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.RuntimeEditing
{
    public class RuntimeEditableObjectRegistry : MonoBehaviour
    {
        [SerializeField] private AvatarRuntimeManager avatarRuntimeManager;

        private readonly Dictionary<string, IRuntimeEditableObject> _editables = new();

        private void Awake()
        {
            ServiceLocator.Register<RuntimeEditableObjectRegistry>(this);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.IsRegistered<RuntimeEditableObjectRegistry>() && ServiceLocator.Get<RuntimeEditableObjectRegistry>() == this)
            {
                ServiceLocator.Unregister<RuntimeEditableObjectRegistry>();
            }
        }

        public void Configure(AvatarRuntimeManager runtimeManager)
        {
            avatarRuntimeManager = runtimeManager;
        }

        public bool TryGetEditable(string objectId, out IRuntimeEditableObject editable)
        {
            Refresh();
            return _editables.TryGetValue(objectId ?? string.Empty, out editable);
        }

        public bool TryGetEditable(GameObject gameObject, out IRuntimeEditableObject editable)
        {
            Refresh();
            foreach (var candidate in _editables.Values)
            {
                if (candidate.Matches(gameObject))
                {
                    editable = candidate;
                    return true;
                }
            }

            editable = null;
            return false;
        }

        private void Refresh()
        {
            if (avatarRuntimeManager == null)
            {
                avatarRuntimeManager = ServiceLocator.IsRegistered<AvatarRuntimeManager>()
                    ? ServiceLocator.Get<AvatarRuntimeManager>()
                    : FindFirstObjectByType<AvatarRuntimeManager>();
            }

            _editables.Clear();
            if (avatarRuntimeManager != null && avatarRuntimeManager.GetManagedAvatarObject() != null)
            {
                var avatarEditable = new RuntimeEditableAvatarAdapter(avatarRuntimeManager);
                _editables[avatarEditable.ObjectId] = avatarEditable;
            }

            var sensors = FindObjectsByType<VirtualSensor>(FindObjectsSortMode.None);
            foreach (var sensor in sensors)
            {
                if (sensor == null || string.IsNullOrWhiteSpace(sensor.name))
                {
                    continue;
                }

                var sensorEditable = new RuntimeEditableSensorAdapter(sensor);
                _editables[sensorEditable.ObjectId] = sensorEditable;
            }
        }
    }
}
