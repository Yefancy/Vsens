using UnityEngine;
using UnityEngine.EventSystems;
using VsensAgent.Core;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.RuntimeEditing
{
    public class RuntimeEditModeController : MonoBehaviour
    {
        [SerializeField] private AvatarRuntimeManager avatarRuntimeManager;
        [SerializeField] private RuntimeEditableObjectRegistry editableRegistry;
        [SerializeField] private SceneRegistry sceneRegistry;
        [SerializeField] private Camera runtimeCamera;
        [SerializeField] private float rotationSensitivity = 140f;

        private IRuntimeEditableObject _selectedEditable;
        private float _selectedYawDegrees;

        public bool IsEditModeEnabled { get; private set; }
        public string SelectedObjectId { get; private set; } = string.Empty;

        private void Awake()
        {
            EnsureDependencies();
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
        }

        public void ToggleEditMode()
        {
            SetEditMode(!IsEditModeEnabled);
        }

        public void SetEditMode(bool enabled)
        {
            EnsureDependencies();
            IsEditModeEnabled = enabled;
            if (!enabled)
            {
                ClearSelection();
            }

            var cameraControl = ServiceLocator.Get<VsensAgent.UserMainCameraControl>() ?? FindFirstObjectByType<VsensAgent.UserMainCameraControl>();
            if (cameraControl != null)
            {
                cameraControl.SetInputEnabled(!enabled);
            }
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
            return true;
        }

        public bool TryMoveSelectionToGroundPoint(Vector3 worldPoint)
        {
            if (!IsEditModeEnabled || _selectedEditable == null)
            {
                return false;
            }

            if (!_selectedEditable.TryMoveToGroundPoint(worldPoint, out _))
            {
                return false;
            }

            RegisterManualMutation("set_avatar_transform");
            return true;
        }

        public bool TryRotateSelectionYaw(float yawDegrees)
        {
            if (!IsEditModeEnabled || _selectedEditable == null)
            {
                return false;
            }

            _selectedYawDegrees = yawDegrees;
            if (!_selectedEditable.TryRotateYaw(yawDegrees, out _))
            {
                return false;
            }

            RegisterManualMutation("set_avatar_transform");
            return true;
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
                        editable != null)
                    {
                        _selectedEditable = editable;
                        SelectedObjectId = editable.ObjectId;
                        _selectedYawDegrees = editable.GetTransform() != null
                            ? AvatarRuntimeManager.GetLogicalRotationEuler(editable.GetTransform().rotation).y
                            : _selectedYawDegrees;
                        return true;
                    }
                }

                return false;
            }

            foreach (var hit in hits)
            {
                if (editableRegistry != null &&
                    editableRegistry.TryGetEditable(hit.collider != null ? hit.collider.gameObject : null, out var editable) &&
                    editable != null)
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
                    return true;
                }

                if (TryMoveSelectionToGroundPoint(hit.point))
                {
                    return true;
                }
            }

            var fallbackPlaneY = _selectedEditable.GetTransform() != null
                ? _selectedEditable.GetTransform().position.y
                : 0f;
            var groundPlane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneY, 0f));
            if (!groundPlane.Raycast(ray, out var enter))
            {
                return false;
            }

            return TryMoveSelectionToGroundPoint(ray.GetPoint(enter));
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

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                TryHandleSelectionOrMove();
            }

            if (_selectedEditable != null && Input.GetMouseButton(1))
            {
                _selectedYawDegrees += Input.GetAxis("Mouse X") * rotationSensitivity * Time.deltaTime;
                TryRotateSelectionYaw(_selectedYawDegrees);
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

        private void ClearSelection()
        {
            _selectedEditable = null;
            SelectedObjectId = string.Empty;
        }

        private void EnsureDependencies()
        {
            if (avatarRuntimeManager == null)
            {
                avatarRuntimeManager = ServiceLocator.Get<AvatarRuntimeManager>() ?? FindFirstObjectByType<AvatarRuntimeManager>();
            }

            if (sceneRegistry == null)
            {
                sceneRegistry = ServiceLocator.Get<SceneRegistry>() ?? FindFirstObjectByType<SceneRegistry>();
            }

            if (editableRegistry == null)
            {
                editableRegistry = ServiceLocator.Get<RuntimeEditableObjectRegistry>() ?? GetComponent<RuntimeEditableObjectRegistry>();
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

            var cameraControl = ServiceLocator.Get<VsensAgent.UserMainCameraControl>() ?? FindFirstObjectByType<VsensAgent.UserMainCameraControl>();
            if (cameraControl != null)
            {
                runtimeCamera = cameraControl.GetComponent<Camera>();
            }

            runtimeCamera ??= Camera.main ?? FindFirstObjectByType<Camera>();
            return runtimeCamera;
        }

        private void RegisterManualMutation(string actionType)
        {
            if (sceneRegistry == null || string.IsNullOrWhiteSpace(SelectedObjectId))
            {
                return;
            }

            sceneRegistry.RegisterMutation("runtime_edit", SelectedObjectId, actionType);
        }
    }
}
