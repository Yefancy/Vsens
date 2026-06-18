using System.Reflection;
using Sensor;
using TransformHandles;
using UnityEngine;
using System;
using VsensAgent.SceneHistory;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
#endif

namespace VsensAgent.RuntimeEditing
{
    public class RuntimeTransformHandleBridge : MonoBehaviour
    {
        private const float DefaultAutoScaleSizeInPixels = 180f;
        private const string RuntimeAssetsResourcePath = "VsensAgent/RuntimeTransformHandleAssets";

        [SerializeField] private RuntimeEditModeController controller;
        [SerializeField] private Camera handleCamera;
        [SerializeField] private bool enableAutoScale = true;
        [SerializeField] private float autoScaleSizeInPixels = DefaultAutoScaleSizeInPixels;
        [SerializeField] private float handleScaleMultiplier = 1.1f;
        [SerializeField] private bool logHandleCreation;

        private TransformHandleManager _handleManager;
        private Handle _activeHandle;
        private string _activeObjectId = string.Empty;
        private Transform _activeTarget;
        private bool _configurationFailed;
        private TransformHandleSettings _runtimeSettings;
        private HandleType _currentHandleType = HandleType.Position;
        private SceneTransformSnapshot _interactionStartSnapshot;

        public Handle ActiveHandle => _activeHandle;
        public HandleType CurrentHandleType => _currentHandleType;
        public bool SupportsCurrentSelection => ResolveController() != null && GetSelectedHandleTarget() != null;

        public void CaptureInteractionStartForTests()
        {
            CaptureInteractionStartSnapshotIfNeeded();
        }

        public void CompleteInteractionForTests()
        {
            OnHandleInteractionEnd(_activeHandle);
        }

        private void Awake()
        {
            controller ??= GetComponent<RuntimeEditModeController>();
        }

        private void OnEnable()
        {
            controller ??= GetComponent<RuntimeEditModeController>();
            if (controller != null)
            {
                controller.SelectionChanged -= OnSelectionChanged;
                controller.SelectionChanged += OnSelectionChanged;
            }

            RefreshHandleBinding();
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.SelectionChanged -= OnSelectionChanged;
            }

            DestroyActiveHandle();
        }

        private void LateUpdate()
        {
            RefreshHandleBinding();
            UpdateActiveHandleScale();
        }

        public void RefreshHandleBinding()
        {
            ResolveController();
            if (controller == null)
            {
                DestroyActiveHandle();
                return;
            }

            if (!controller.IsEditModeEnabled)
            {
                DestroyActiveHandle();
                return;
            }

            var selectedObjectId = controller.SelectedObjectId;
            var target = GetSelectedHandleTarget();
            if (target == null)
            {
                DestroyActiveHandle();
                return;
            }

            if (_activeHandle != null && _activeObjectId == selectedObjectId && _activeTarget == target)
            {
                return;
            }

            if (_configurationFailed)
            {
                return;
            }

            DestroyActiveHandle();

            var manager = EnsureHandleManager();
            if (manager == null)
            {
                return;
            }

            Handle handle = null;
            try
            {
                handle = manager.CreateHandle(target);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RuntimeTransformHandleBridge] CreateHandle threw for '{target.name}': {ex}");
                handle = TryCreateFallbackHandle(manager, target);
            }

            if (handle == null)
            {
                Debug.LogWarning($"[RuntimeTransformHandleBridge] CreateHandle returned null for '{target.name}'.");
                handle = TryCreateFallbackHandle(manager, target);
                if (handle == null)
                {
                    return;
                }
            }

            try
            {
                ApplyHandleType(handle);
                ApplyHandleDisplaySettings(handle);
                handle.OnInteractionStartEvent += OnHandleInteractionStart;
                handle.OnInteractionEvent += OnHandleInteraction;
                handle.OnInteractionEndEvent += OnHandleInteractionEnd;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RuntimeTransformHandleBridge] Handle post-config threw for '{target.name}': {ex}");
                if (handle != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(handle.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(handle.gameObject);
                    }
                }
                return;
            }

            _activeHandle = handle;
            _activeObjectId = selectedObjectId;
            _activeTarget = target;
            if (logHandleCreation)
            {
                Debug.Log($"[RuntimeTransformHandleBridge] Created {handle.type} handle for '{selectedObjectId}' using camera '{ResolveCamera()?.name ?? "none"}'.");
            }
        }

        public void SetHandleType(HandleType handleType)
        {
            _currentHandleType = handleType;
            if (_activeHandle == null)
            {
                return;
            }

            ApplyHandleType(_activeHandle);
        }

        public void ResetConfigurationFailure()
        {
            _configurationFailed = false;
        }

        private void OnSelectionChanged(string _)
        {
            ResetConfigurationFailure();
            RefreshHandleBinding();
        }

        private RuntimeEditModeController ResolveController()
        {
            if (controller == null)
            {
                controller = GetComponent<RuntimeEditModeController>();
            }

            return controller;
        }

        private void OnHandleInteractionStart(Handle _)
        {
            CaptureInteractionStartSnapshotIfNeeded();
        }

        private void OnHandleInteraction(Handle _)
        {
            CaptureInteractionStartSnapshotIfNeeded();
        }

        private void CaptureInteractionStartSnapshotIfNeeded()
        {
            if (_interactionStartSnapshot != null)
            {
                return;
            }

            if (_activeTarget == null)
            {
                _interactionStartSnapshot = null;
                return;
            }

            _interactionStartSnapshot = SceneTransformSnapshot.Capture(_activeObjectId, _activeTarget);
        }

        private void OnHandleInteractionEnd(Handle _)
        {
            controller?.NotifySelectedObjectMutated();
            if (_activeTarget == null || _interactionStartSnapshot == null)
            {
                return;
            }

            var actionType = _activeTarget.GetComponentInParent<VirtualSensor>() != null
                ? "set_sensor"
                : "set_avatar_transform";
            SceneActionHistory.GetOrCreate()?.TryRecordTransformChange(
                "user",
                actionType,
                _activeObjectId,
                _interactionStartSnapshot,
                SceneTransformSnapshot.Capture(_activeObjectId, _activeTarget));
            _interactionStartSnapshot = null;
        }

        private TransformHandleManager EnsureHandleManager()
        {
            if (_handleManager != null)
            {
                EnsureManagerInitialized(_handleManager);
                _handleManager.mainCamera = ResolveCamera();
                return _handleManager;
            }

            _handleManager = TransformHandleManager.Instance;
            if (_handleManager == null)
            {
                return null;
            }

            if (!ConfigureHandleManager(_handleManager))
            {
                _configurationFailed = true;
                return null;
            }

            EnsureManagerInitialized(_handleManager);
            _handleManager.mainCamera = ResolveCamera();
            return _handleManager;
        }

        private static void EnsureManagerInitialized(TransformHandleManager manager)
        {
            if (manager == null)
            {
                return;
            }

            var initializedField = manager.GetType().GetField("_isInitialized", BindingFlags.Instance | BindingFlags.NonPublic);
            if (initializedField != null && initializedField.GetValue(manager) is bool isInitialized && isInitialized)
            {
                return;
            }

            var initializeMethod = manager.GetType().GetMethod("InitializeManager", BindingFlags.Instance | BindingFlags.NonPublic);
            initializeMethod?.Invoke(manager, null);
        }

        private Camera ResolveCamera()
        {
            if (handleCamera != null)
            {
                return handleCamera;
            }

            handleCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            return handleCamera;
        }

        private Transform GetSelectedHandleTarget()
        {
            var selectedTransform = controller != null ? controller.GetSelectedTransform() : null;
            if (selectedTransform == null)
            {
                return null;
            }

            var sensor = selectedTransform.GetComponentInParent<VirtualSensor>();
            return sensor != null ? sensor.transform : selectedTransform;
        }

        private void ApplyHandleDisplaySettings(Handle handle)
        {
            if (handle == null)
            {
                return;
            }

            handle.handleCamera = ResolveCamera();
            handle.autoScale = false;
            handle.AutoScaleSizeInPixels = autoScaleSizeInPixels;
            handle.ScaleMultiplier = handleScaleMultiplier;
        }

        private void ApplyHandleType(Handle handle)
        {
            if (handle == null)
            {
                return;
            }

            TransformHandleManager.ChangeHandleType(handle, _currentHandleType);
            handle.axes = HandleAxes.XYZ;
            handle.space = _currentHandleType == HandleType.Rotation ? Space.Self : Space.World;
        }

        private void UpdateActiveHandleScale()
        {
            if (_activeHandle == null)
            {
                return;
            }

            var camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            if (!enableAutoScale)
            {
                _activeHandle.transform.localScale = Vector3.one * handleScaleMultiplier;
                return;
            }

            float distance = GetDistanceAlongView(camera, _activeHandle.transform.position);
            float worldSize;
            if (camera.orthographic)
            {
                worldSize = (camera.orthographicSize * 2f) * (autoScaleSizeInPixels / Mathf.Max(1f, Screen.height));
            }
            else
            {
                worldSize = 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance * (autoScaleSizeInPixels / Mathf.Max(1f, Screen.height));
            }

            _activeHandle.transform.localScale = Vector3.one * worldSize * handleScaleMultiplier;
        }

        private static float GetDistanceAlongView(Camera camera, Vector3 worldPosition)
        {
            var toTarget = worldPosition - camera.transform.position;
            float distance = Vector3.Dot(toTarget, camera.transform.forward);
            if (distance <= 0f)
            {
                distance = toTarget.magnitude;
            }

            return Mathf.Max(0.05f, distance);
        }

        private bool ConfigureHandleManager(TransformHandleManager manager)
        {
            if (!TryConfigureRuntimePrefabs(manager, out var prefabError))
            {
#if UNITY_EDITOR
                if (!TryConfigureEditorFallbackPrefabs(manager, out var editorFallbackError))
                {
                    Debug.LogError($"[RuntimeTransformHandleBridge] Runtime transform handle prefabs are unavailable. {prefabError} Editor fallback also failed: {editorFallbackError}");
                    return false;
                }
#else
                Debug.LogError($"[RuntimeTransformHandleBridge] Runtime transform handle prefabs are unavailable. {prefabError} Mac/Player builds require Resources asset '{RuntimeAssetsResourcePath}' with valid prefabs.");
                return false;
#endif
            }

            EnsureRuntimeSettings();
            SetPrivateField(manager, "settings", _runtimeSettings);
            SetPrivateField(manager, "layerMask", (LayerMask)(~0));
            SetPrivateField(manager, "handleLayerName", string.Empty);
            return true;
        }

        private static bool TryConfigureRuntimePrefabs(TransformHandleManager manager, out string error)
        {
            if (manager == null)
            {
                error = "TransformHandleManager is null.";
                return false;
            }

            var assets = Resources.Load<RuntimeTransformHandleAssetSet>(RuntimeAssetsResourcePath);
            if (assets == null)
            {
                error = $"Resources.Load<{nameof(RuntimeTransformHandleAssetSet)}>(\"{RuntimeAssetsResourcePath}\") returned null.";
                return false;
            }

            if (!assets.IsValid(out error))
            {
                return false;
            }

            SetPrivateField(manager, "transformHandlePrefab", assets.TransformHandlePrefab);
            SetPrivateField(manager, "ghostPrefab", assets.GhostPrefab);
            error = null;
            return true;
        }

#if UNITY_EDITOR
        private static bool TryConfigureEditorFallbackPrefabs(TransformHandleManager manager, out string error)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TransformHandleManager).Assembly);
            if (packageInfo == null)
            {
                error = "Could not locate transform handles package info.";
                return false;
            }

            var packageAssetRoot = $"Packages/{packageInfo.name}";
            var handlePrefabPath = $"{packageAssetRoot}/Runtime/Prefabs/NativeTransformHandle.prefab";
            var ghostPrefabPath = $"{packageAssetRoot}/Runtime/Prefabs/Ghost.prefab";

            var handlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(handlePrefabPath);
            var ghostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ghostPrefabPath);

            if (handlePrefab == null || ghostPrefab == null)
            {
                error = $"Failed to load handle prefabs. handle='{handlePrefabPath}', ghost='{ghostPrefabPath}'.";
                return false;
            }

            SetPrivateField(manager, "transformHandlePrefab", handlePrefab);
            SetPrivateField(manager, "ghostPrefab", ghostPrefab);
            error = null;
            return true;
        }
#endif

        private void EnsureRuntimeSettings()
        {
            if (_runtimeSettings != null)
            {
                return;
            }

            _runtimeSettings = TransformHandleSettings.CreateDefault();
            _runtimeSettings.hideFlags = HideFlags.HideAndDontSave;
            SetPrivateField(_runtimeSettings, "enableShortcuts", false);
            SetPrivateField(_runtimeSettings, "autoScaleHandles", enableAutoScale);
            SetPrivateField(_runtimeSettings, "handleScale", handleScaleMultiplier);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return;
            }

            field.SetValue(target, value);
        }

        private static Handle TryCreateFallbackHandle(TransformHandleManager manager, Transform target)
        {
            if (manager == null || target == null)
            {
                return null;
            }

            var prefabField = manager.GetType().GetField("transformHandlePrefab", BindingFlags.Instance | BindingFlags.NonPublic);
            var prefab = prefabField?.GetValue(manager) as GameObject;
            if (prefab == null)
            {
                Debug.LogWarning("[RuntimeTransformHandleBridge] No transformHandlePrefab available for fallback handle creation.");
                return null;
            }

            var instance = Instantiate(prefab);
            var handle = instance != null ? instance.GetComponent<Handle>() : null;
            if (handle == null)
            {
                if (instance != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(instance);
                    }
                    else
                    {
                        DestroyImmediate(instance);
                    }
                }

                Debug.LogWarning("[RuntimeTransformHandleBridge] Fallback handle prefab does not contain a Handle component.");
                return null;
            }

            handle.Enable(target);
            return handle;
        }

        private void DestroyActiveHandle()
        {
            if (_activeHandle != null)
            {
                _activeHandle.OnInteractionStartEvent -= OnHandleInteractionStart;
                _activeHandle.OnInteractionEvent -= OnHandleInteraction;
                _activeHandle.OnInteractionEndEvent -= OnHandleInteractionEnd;
                if (Application.isPlaying)
                {
                    Destroy(_activeHandle.gameObject);
                }
                else
                {
                    DestroyImmediate(_activeHandle.gameObject);
                }
            }

            _activeHandle = null;
            _activeObjectId = string.Empty;
            _activeTarget = null;
            _interactionStartSnapshot = null;
        }
    }
}
