using UnityEngine;
using UnityEngine.EventSystems;
using VsensAgent.Core;
using VsensAgent.SceneApi.V2;
using System;
using System.Collections.Generic;
using VsensAgent.SceneHistory;

namespace VsensAgent.RuntimeEditing
{
    public class RuntimeEditModeController : MonoBehaviour
    {
        [SerializeField] private AvatarRuntimeManager avatarRuntimeManager;
        [SerializeField] private RuntimeEditableObjectRegistry editableRegistry;
        [SerializeField] private SceneRegistry sceneRegistry;
        [SerializeField] private Camera runtimeCamera;

        private IRuntimeEditableObject _selectedEditable;
        private float _selectedYawDegrees;
        private readonly List<RaycastResult> _uiRaycastResults = new();

        public bool IsEditModeEnabled { get; private set; }
        public string SelectedObjectId { get; private set; } = string.Empty;
        public event Action<string> SelectionChanged;

        private void Awake()
        {
            EnsureDependencies();
            EnsureTransformHandleBridge();
            ServiceLocator.Register<RuntimeEditModeController>(this);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.IsRegistered<RuntimeEditModeController>() && ServiceLocator.Get<RuntimeEditModeController>() == this)
            {
                ServiceLocator.Unregister<RuntimeEditModeController>();
            }
        }

        public void Configure(AvatarRuntimeManager runtimeManager)
        {
            avatarRuntimeManager = runtimeManager;
            EnsureDependencies();
            EnsureTransformHandleBridge();
        }

        public void ToggleEditMode()
        {
            SetEditMode(!IsEditModeEnabled);
        }

        public void SetEditMode(bool enabled)
        {
            EnsureDependencies();
            EnsureTransformHandleBridge();
            IsEditModeEnabled = enabled;
            if (!enabled)
            {
                ClearSelection();
            }
        }

        public Transform GetSelectedTransform()
        {
            return _selectedEditable?.GetTransform();
        }

        public void NotifySelectedObjectMutated(string actionType = null)
        {
            if (_selectedEditable == null)
            {
                return;
            }

            var effectiveActionType = string.IsNullOrWhiteSpace(actionType)
                ? (_selectedEditable is RuntimeEditableSensorAdapter ? "set_sensor" : "set_avatar_transform")
                : actionType;

            _selectedEditable.CommitTransformMutation(out _);
            RegisterManualMutation(effectiveActionType);
        }

        public bool TrySelectEditable(string objectId)
        {
            EnsureDependencies();
            if (!IsEditModeEnabled || editableRegistry == null)
            {
                return false;
            }

            if (!editableRegistry.TryGetEditable(objectId, out var editable) || editable == null)
            {
                return false;
            }

            _selectedEditable = editable;
            SelectedObjectId = editable.ObjectId;
            _selectedYawDegrees = editable.GetTransform() != null
                ? AvatarRuntimeManager.GetLogicalRotationEuler(editable.GetTransform().rotation).y
                : 0f;
            SelectionChanged?.Invoke(SelectedObjectId);
            return true;
        }

        public bool TryMoveSelectionToGroundPoint(Vector3 worldPoint)
        {
            if (!IsEditModeEnabled || _selectedEditable == null)
            {
                return false;
            }

            if (_selectedEditable is RuntimeEditableSensorAdapter)
            {
                return false;
            }

            var transform = _selectedEditable.GetTransform();
            var before = SceneTransformSnapshot.Capture(SelectedObjectId, transform);
            if (!_selectedEditable.TryMoveToGroundPoint(worldPoint, out _))
            {
                return false;
            }

            RegisterManualMutation("set_avatar_transform");
            RecordManualTransformChange("set_avatar_transform", before);
            return true;
        }

        public bool TryRotateSelectionYaw(float yawDegrees)
        {
            if (!IsEditModeEnabled || _selectedEditable == null)
            {
                return false;
            }

            if (_selectedEditable is RuntimeEditableSensorAdapter)
            {
                return false;
            }

            var transform = _selectedEditable.GetTransform();
            var before = SceneTransformSnapshot.Capture(SelectedObjectId, transform);
            _selectedYawDegrees = yawDegrees;
            if (!_selectedEditable.TryRotateYaw(yawDegrees, out _))
            {
                return false;
            }

            RegisterManualMutation("set_avatar_transform");
            RecordManualTransformChange("set_avatar_transform", before);
            return true;
        }

        public bool TrySelectEditableFromUi(string objectId)
        {
            return TrySelectEditable(objectId);
        }

        public void ClearSelectionIfSelected(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId) || !string.Equals(SelectedObjectId, objectId, StringComparison.Ordinal))
            {
                return;
            }

            ClearSelection();
        }

        public bool TryHandlePointerRay(Ray ray)
        {
            if (!IsEditModeEnabled)
            {
                return false;
            }

            var hits = Physics.RaycastAll(ray, 100f);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            if (_selectedEditable == null)
            {
                foreach (var hit in hits)
                {
                    if (editableRegistry != null &&
                        editableRegistry.TryGetEditable(hit.collider != null ? hit.collider.gameObject : null, out var editable) &&
                        editable != null &&
                        IsSceneSelectable(editable))
                    {
                        _selectedEditable = editable;
                        SelectedObjectId = editable.ObjectId;
                        _selectedYawDegrees = editable.GetTransform() != null
                            ? AvatarRuntimeManager.GetLogicalRotationEuler(editable.GetTransform().rotation).y
                            : _selectedYawDegrees;
                        SelectionChanged?.Invoke(SelectedObjectId);
                        return true;
                    }
                }

                return false;
            }

            if (_selectedEditable is RuntimeEditableSensorAdapter)
            {
                foreach (var hit in hits)
                {
                    if (editableRegistry != null &&
                        editableRegistry.TryGetEditable(hit.collider != null ? hit.collider.gameObject : null, out var editable) &&
                        editable != null &&
                        IsSceneSelectable(editable) &&
                        editable.ObjectId != _selectedEditable.ObjectId)
                    {
                        _selectedEditable = editable;
                        SelectedObjectId = editable.ObjectId;
                        _selectedYawDegrees = editable.GetTransform() != null
                            ? AvatarRuntimeManager.GetLogicalRotationEuler(editable.GetTransform().rotation).y
                            : _selectedYawDegrees;
                        SelectionChanged?.Invoke(SelectedObjectId);
                        return true;
                    }
                }

                return false;
            }

            foreach (var hit in hits)
            {
                if (editableRegistry != null &&
                    editableRegistry.TryGetEditable(hit.collider != null ? hit.collider.gameObject : null, out var editable) &&
                    editable != null &&
                    IsSceneSelectable(editable))
                {
                    if (editable.ObjectId == _selectedEditable.ObjectId)
                    {
                        continue;
                    }

                    _selectedEditable = editable;
                    SelectedObjectId = editable.ObjectId;
                    _selectedYawDegrees = editable.GetTransform() != null
                        ? AvatarRuntimeManager.GetLogicalRotationEuler(editable.GetTransform().rotation).y
                        : _selectedYawDegrees;
                    SelectionChanged?.Invoke(SelectedObjectId);
                    return true;
                }

                if (editableRegistry != null &&
                    editableRegistry.TryGetEditable(hit.collider != null ? hit.collider.gameObject : null, out var sameEditable) &&
                    sameEditable != null &&
                    _selectedEditable != null &&
                    sameEditable.ObjectId == _selectedEditable.ObjectId)
                {
                    continue;
                }

                return false;
            }

            return false;
        }

        private static bool IsSceneSelectable(IRuntimeEditableObject editable)
        {
            return editable is not RuntimeEditableAvatarAdapter;
        }

        public void OverrideRuntimeCameraForTests(Camera camera)
        {
            runtimeCamera = camera;
        }

        private void Update()
        {
            if (!IsEditModeEnabled)
            {
                return;
            }

            if (IsPointerBlockedByUi())
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                TryHandleSelectionOrMove();
            }

        }

        private void TryHandleSelectionOrMove()
        {
            var camera = ResolveRuntimeCamera();
            if (camera == null)
            {
                return;
            }

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            TryHandlePointerRay(ray);
        }

        private bool IsPointerBlockedByUi()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            // Legacy and common desktop path.
            if (eventSystem.IsPointerOverGameObject())
            {
                return true;
            }

            // Touch path requires explicit finger id.
            for (var index = 0; index < Input.touchCount; index++)
            {
                if (eventSystem.IsPointerOverGameObject(Input.GetTouch(index).fingerId))
                {
                    return true;
                }
            }

            // Fallback for modules where IsPointerOverGameObject can miss edge cases.
            var pointerData = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }

        private void ClearSelection()
        {
            _selectedEditable = null;
            SelectedObjectId = string.Empty;
            SelectionChanged?.Invoke(string.Empty);
        }

        private void EnsureDependencies()
        {
            if (avatarRuntimeManager == null)
            {
                avatarRuntimeManager = ServiceLocator.IsRegistered<AvatarRuntimeManager>()
                    ? ServiceLocator.Get<AvatarRuntimeManager>()
                    : FindFirstObjectByType<AvatarRuntimeManager>();
            }

            if (sceneRegistry == null)
            {
                sceneRegistry = ServiceLocator.IsRegistered<SceneRegistry>()
                    ? ServiceLocator.Get<SceneRegistry>()
                    : FindFirstObjectByType<SceneRegistry>();
            }

            if (editableRegistry == null)
            {
                editableRegistry = ServiceLocator.IsRegistered<RuntimeEditableObjectRegistry>()
                    ? ServiceLocator.Get<RuntimeEditableObjectRegistry>()
                    : GetComponent<RuntimeEditableObjectRegistry>();
                if (editableRegistry == null)
                {
                    editableRegistry = gameObject.AddComponent<RuntimeEditableObjectRegistry>();
                }
            }

            editableRegistry.Configure(avatarRuntimeManager);
            runtimeCamera = ResolveRuntimeCamera();
        }

        private Camera ResolveRuntimeCamera()
        {
            if (runtimeCamera != null)
            {
                return runtimeCamera;
            }

            var cameraControl = ServiceLocator.IsRegistered<VsensAgent.UserMainCameraControl>()
                ? ServiceLocator.Get<VsensAgent.UserMainCameraControl>()
                : FindFirstObjectByType<VsensAgent.UserMainCameraControl>();
            if (cameraControl != null)
            {
                runtimeCamera = cameraControl.GetComponent<Camera>();
            }

            runtimeCamera ??= Camera.main ?? FindFirstObjectByType<Camera>();
            return runtimeCamera;
        }

        private void EnsureTransformHandleBridge()
        {
            if (GetComponent<RuntimeTransformHandleBridge>() == null)
            {
                gameObject.AddComponent<RuntimeTransformHandleBridge>();
            }
        }

        private void RegisterManualMutation(string actionType)
        {
            if (sceneRegistry == null || string.IsNullOrWhiteSpace(SelectedObjectId))
            {
                return;
            }

            var mutationType = _selectedEditable is RuntimeEditableSensorAdapter ? "set_sensor" : actionType;
            sceneRegistry.RegisterMutation("runtime_edit", SelectedObjectId, mutationType);
        }

        private void RecordManualTransformChange(string actionType, SceneTransformSnapshot before)
        {
            if (_selectedEditable == null || before == null)
            {
                return;
            }

            var transform = _selectedEditable.GetTransform();
            SceneActionHistory.GetOrCreate()?.TryRecordTransformChange(
                "user",
                actionType,
                SelectedObjectId,
                before,
                SceneTransformSnapshot.Capture(SelectedObjectId, transform));
        }
    }
}
