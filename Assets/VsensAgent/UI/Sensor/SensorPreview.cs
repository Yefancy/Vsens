using System.Collections.Generic;
using Sensor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VsensAgent.Core;
using VsensAgent.SceneApi.V2;
using VsensAgent.VirtualObject.Sensor;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VsensAgent.UI.Sensor
{
    public class SensorPreview : MonoBehaviour
    {
        [Header("Placement References")]
        public CanvasGroup workspaceCanvasGroup;
        public VirtualSensor sensorPrefab;
        public Button addButton;
        public MonitorManager monitorManager;

        [Header("Placement Settings")]
        public float maxRayDistance = 100f;
        public float surfaceOffset = 0.005f;
        public LayerMask placementMask = ~0;

        private Camera runtimeCamera;
        private VsensAgentSensorManager sensorManager;
        private SceneRegistry sceneRegistry;
        private GameObject previewInstance;
        private bool isPlacing;
        private float workspaceOriginalAlpha = 1f;
        private bool workspaceOriginalInteractable = true;
        private bool workspaceOriginalBlocksRaycasts = true;
        private readonly List<RaycastResult> uiRaycastResults = new();

        public bool IsPlacing => isPlacing;

        private void Awake()
        {
            if (addButton != null)
            {
                addButton.onClick.RemoveListener(BeginPlacement);
                addButton.onClick.AddListener(BeginPlacement);
            }
        }

        private void OnDestroy()
        {
            if (addButton != null)
            {
                addButton.onClick.RemoveListener(BeginPlacement);
            }

            DestroyPreviewInstance();
        }

        private void Update()
        {
            if (!isPlacing)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            if (IsPointerBlockedByUi())
            {
                SetPreviewVisible(false);
                return;
            }

            if (!TryGetPlacementHit(out var hit))
            {
                SetPreviewVisible(false);
                return;
            }

            UpdatePreviewPose(hit);

            if (Input.GetMouseButtonDown(0))
            {
                TryPlaceSensorAtHit(hit);
            }
        }

        public void BeginPlacement()
        {
            if (isPlacing || sensorPrefab == null)
            {
                return;
            }

            EnsureDependencies();
            isPlacing = true;
            CacheWorkspaceState();
            SetWorkspaceVisible(false);

            EnsurePreviewInstance();
            SetPreviewVisible(false);
        }

        public void CancelPlacement()
        {
            if (!isPlacing)
            {
                return;
            }

            EndPlacement();
        }

        public bool TryPlaceSensorAtHit(RaycastHit hit)
        {
            if (!isPlacing || sensorPrefab == null)
            {
                return false;
            }

            var createdSensor = CreateSensorInstance();
            if (createdSensor == null)
            {
                Debug.LogWarning("[SensorPreview] Failed to create sensor instance.");
                EndPlacement();
                return false;
            }

            var attachTransform = hit.collider != null ? hit.collider.transform : null;
            var placementPosition = hit.point + hit.normal * surfaceOffset;
            var placementRotation = Quaternion.LookRotation(hit.normal, Vector3.up);

            var createdTransform = createdSensor.transform;
            createdTransform.SetPositionAndRotation(placementPosition, placementRotation);
            if (attachTransform != null)
            {
                createdTransform.SetParent(attachTransform, true);
            }

            sceneRegistry?.RegisterMutation("ui.sensor_preview", createdSensor.gameObject.name, "set_sensor");
            monitorManager?.RefreshSensorList();
            EndPlacement();
            return true;
        }

        private void EndPlacement()
        {
            isPlacing = false;
            DestroyPreviewInstance();
            RestoreWorkspaceState();
        }

        private bool TryGetPlacementHit(out RaycastHit hit)
        {
            hit = default;
            var camera = ResolveRuntimeCamera();
            if (camera == null)
            {
                return false;
            }

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            return Physics.Raycast(ray, out hit, maxRayDistance, placementMask, QueryTriggerInteraction.Ignore);
        }

        private void UpdatePreviewPose(RaycastHit hit)
        {
            EnsurePreviewInstance();
            if (previewInstance == null)
            {
                return;
            }

            previewInstance.transform.SetPositionAndRotation(
                hit.point + hit.normal * surfaceOffset,
                Quaternion.LookRotation(hit.normal, Vector3.up));
            SetPreviewVisible(true);
        }

        private void EnsurePreviewInstance()
        {
            if (previewInstance != null || sensorPrefab == null)
            {
                return;
            }

            previewInstance = Instantiate(sensorPrefab.gameObject);
            if (previewInstance.TryGetComponent<VirtualSensor>(out var sensor))
            {
                sensor.isPreview = true;
                DestroyUnityObject(sensor);
            }

            previewInstance.name = $"{sensorPrefab.gameObject.name}_Preview";

            foreach (var behaviour in previewInstance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (var collider in previewInstance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var body in previewInstance.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }

        private void DestroyPreviewInstance()
        {
            if (previewInstance == null)
            {
                return;
            }

            DestroyUnityObject(previewInstance);

            previewInstance = null;
        }

        private void SetPreviewVisible(bool visible)
        {
            if (previewInstance != null && previewInstance.activeSelf != visible)
            {
                previewInstance.SetActive(visible);
            }
        }

        private VirtualSensor CreateSensorInstance()
        {
            EnsureDependencies();
            VirtualSensor createdSensor = null;

            if (sensorManager != null)
            {
                var sensorType = sensorPrefab.SensorDefinition()?.getSensorName();
                if (!string.IsNullOrWhiteSpace(sensorType))
                {
                    createdSensor = sensorManager.CreateSensorByName(sensorType, null);
                }
            }

            if (createdSensor != null)
            {
                createdSensor.isPreview = false;
                sensorManager?.EnsureSensorObjectName(createdSensor);
                return createdSensor;
            }

            createdSensor = Instantiate(sensorPrefab);
            createdSensor.prefab = sensorPrefab.gameObject;
            createdSensor.isPreview = false;
            sensorManager?.EnsureSensorObjectName(createdSensor);
            SensorDataCenter.Instance?.RegisterSensor(createdSensor);
            return createdSensor;
        }

        private static void DestroyUnityObject(Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying || !EditorApplication.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private void EnsureDependencies()
        {
            monitorManager ??= FindFirstObjectByType<MonitorManager>();
            sensorManager = VsensAgentSensorManager.Instance
                ?? (ServiceLocator.IsRegistered<VsensAgentSensorManager>()
                    ? ServiceLocator.Get<VsensAgentSensorManager>()
                    : FindFirstObjectByType<VsensAgentSensorManager>());
            sceneRegistry ??= ServiceLocator.IsRegistered<SceneRegistry>()
                ? ServiceLocator.Get<SceneRegistry>()
                : FindFirstObjectByType<SceneRegistry>();
            runtimeCamera = ResolveRuntimeCamera();
        }

        private void CacheWorkspaceState()
        {
            if (workspaceCanvasGroup == null)
            {
                return;
            }

            workspaceOriginalAlpha = workspaceCanvasGroup.alpha;
            workspaceOriginalInteractable = workspaceCanvasGroup.interactable;
            workspaceOriginalBlocksRaycasts = workspaceCanvasGroup.blocksRaycasts;
        }

        private void RestoreWorkspaceState()
        {
            if (workspaceCanvasGroup == null)
            {
                return;
            }

            workspaceCanvasGroup.alpha = workspaceOriginalAlpha;
            workspaceCanvasGroup.interactable = workspaceOriginalInteractable;
            workspaceCanvasGroup.blocksRaycasts = workspaceOriginalBlocksRaycasts;
        }

        private void SetWorkspaceVisible(bool visible)
        {
            if (workspaceCanvasGroup == null)
            {
                return;
            }

            workspaceCanvasGroup.alpha = visible ? workspaceOriginalAlpha : 0f;
            workspaceCanvasGroup.interactable = visible && workspaceOriginalInteractable;
            workspaceCanvasGroup.blocksRaycasts = visible && workspaceOriginalBlocksRaycasts;
        }

        private Camera ResolveRuntimeCamera()
        {
            if (runtimeCamera != null)
            {
                return runtimeCamera;
            }

            var cameraControl = ServiceLocator.IsRegistered<UserMainCameraControl>()
                ? ServiceLocator.Get<UserMainCameraControl>()
                : FindFirstObjectByType<UserMainCameraControl>();
            if (cameraControl != null)
            {
                runtimeCamera = cameraControl.GetComponent<Camera>();
            }

            runtimeCamera ??= Camera.main ?? FindFirstObjectByType<Camera>();
            return runtimeCamera;
        }

        private bool IsPointerBlockedByUi()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (eventSystem.IsPointerOverGameObject())
            {
                return true;
            }

            var pointerData = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition
            };
            uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, uiRaycastResults);
            return uiRaycastResults.Count > 0;
        }
    }
}
