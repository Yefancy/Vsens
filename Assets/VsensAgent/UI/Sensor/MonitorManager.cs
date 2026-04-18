using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VsensAgent.Core;
using VsensAgent.RuntimeEditing;
using VsensAgent.SceneApi.V2;
using VsensAgent.VirtualObject.Sensor;
using Sensor;

namespace VsensAgent.UI
{
    /// <summary>
    /// 管理传感器监控面板UI，显示场景中所有传感器的列表和数据
    /// </summary>
    public class MonitorManager : MonoBehaviour
    {
        [Header("UI Panel References")]
        public GameObject monitorPanel;           // 整个传感器监控面板
        public ScrollRect scrollRect;         // 传感器列表滚动区域
        public Transform itemContainer;           // 传感器Item的容器 (ScrollRect的Content)
        
        [Header("Prefabs")]
        public GameObject sensorItemPrefab;             // 传感器Item预制件
        public GameObject avatarItemPrefab;             // AvatarItem预制件

        [Header("Recording UI")]
        public Button recordingButton;
        public Image recordingButtonImage;
        public TMP_Text recordingTimerText;
        public Color recordingButtonIdleColor = Color.white;
        public Color recordingButtonRecordingColor = new Color(0.85f, 0.2f, 0.2f, 1f);
        
        [Header("Settings")]
        public float updateInterval = 0.5f;             // 数据更新间隔（秒）
        public bool autoRefreshList = true;             // 自动刷新传感器列表
        public float listRefreshInterval = 2f;          // 列表刷新间隔（秒）
        
        [Header("Debug")]
        public bool showDebugInfo = false;               // 显示调试信息
        
        // 私有变量
        private VsensAgentSensorManager sensorManager;
        private Dictionary<string, SensorUIItem> sensorUIItems = new Dictionary<string, SensorUIItem>();
        private Dictionary<string, AvatarUIItem> avatarUIItems = new Dictionary<string, AvatarUIItem>();
        private AvatarRuntimeManager avatarRuntimeManager;
        private SceneRegistry sceneRegistry;
        private float nextUpdateTime;
        private float nextRefreshTime;
        private bool isPanelVisible = true;

        void Awake()
        {
            // 确保组件引用
            if (scrollRect == null)
                scrollRect = GetComponentInChildren<ScrollRect>();
            
            if (itemContainer == null && scrollRect != null)
                itemContainer = scrollRect.content;

            if (recordingButtonImage == null && recordingButton != null)
                recordingButtonImage = recordingButton.GetComponent<Image>();
        }

        void Start()
        {
            // 获取传感器管理器实例
            sensorManager = VsensAgentSensorManager.Instance
                ?? (ServiceLocator.IsRegistered<VsensAgentSensorManager>()
                    ? ServiceLocator.Get<VsensAgentSensorManager>()
                    : FindFirstObjectByType<VsensAgentSensorManager>());
            avatarRuntimeManager = ServiceLocator.IsRegistered<AvatarRuntimeManager>()
                ? ServiceLocator.Get<AvatarRuntimeManager>()
                : FindFirstObjectByType<AvatarRuntimeManager>();
            sceneRegistry = ServiceLocator.IsRegistered<SceneRegistry>()
                ? ServiceLocator.Get<SceneRegistry>()
                : FindFirstObjectByType<SceneRegistry>();
            
            if (sensorManager == null)
            {
                Debug.LogError("[SensorMonitorManager] ❌ VsensAgentSensorManager instance not found!");
            }
            
            if (monitorPanel != null)
            {
                monitorPanel.SetActive(isPanelVisible);
            }
            
            // 初始化传感器列表
            RefreshSensorList();
            BindRecordingUi();
            RefreshRecordingUi();
            
            if (showDebugInfo)
                Debug.Log("[SensorMonitorManager] ✅ Sensor Monitor initialized");
        }

        void Update()
        {
            if (sensorManager == null) return;
            
            // 定时更新传感器数据
            if (Time.time >= nextUpdateTime)
            {
                UpdateAllSensorData();
                nextUpdateTime = Time.time + updateInterval;
            }
            
            // 定时刷新传感器列表（检测新增/删除的传感器）
            if (autoRefreshList && Time.time >= nextRefreshTime)
            {
                RefreshSensorList();
                nextRefreshTime = Time.time + listRefreshInterval;
            }

            RefreshRecordingUi();
        }

        /// <summary>
        /// 刷新传感器列表 - 重新获取所有传感器并更新UI
        /// </summary>
        public void RefreshSensorList()
        {
            if (sensorManager == null || itemContainer == null)
            {
                Debug.LogWarning("[SensorMonitorManager] ⚠️ Cannot refresh sensor list - missing references");
                if (itemContainer == null)
                {
                    return;
                }
            }
            
            // 获取场景中所有的VirtualSensor
            var allSensors = FindObjectsByType<VirtualSensor>(FindObjectsSortMode.None);
            
            if (showDebugInfo)
            {
                Debug.Log($"[SensorMonitorManager] 🔍 Found {allSensors.Length} sensors in scene");
            }
            
            // 记录当前的传感器名称
            HashSet<string> currentSensorNames = new HashSet<string>();
            
            foreach (var sensor in allSensors)
            {
                if (sensor == null) continue;
                
                string sensorName = sensor.name;
                currentSensorNames.Add(sensorName);
                
                // 如果UI中还没有这个传感器，创建新的UI Item
                if (!sensorUIItems.ContainsKey(sensorName))
                {
                    CreateSensorUIItem(sensor);
                }
                else
                {
                    // 更新现有的UI Item引用
                    sensorUIItems[sensorName].UpdateSensorReference(sensor);
                }
            }
            
            // 移除已经不存在的传感器UI
            List<string> toRemove = new List<string>();
            foreach (var kvp in sensorUIItems)
            {
                if (!currentSensorNames.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                    if (kvp.Value != null && kvp.Value.gameObject != null)
                    {
                        Destroy(kvp.Value.gameObject);
                    }
                }
            }
            
            foreach (var key in toRemove)
            {
                sensorUIItems.Remove(key);
            }
            
            if (showDebugInfo && toRemove.Count > 0)
            {
                Debug.Log($"[SensorMonitorManager] 🗑️ Removed {toRemove.Count} sensor UI items");
            }

            RefreshAvatarList();
        }

        /// <summary>
        /// 为传感器创建UI Item
        /// </summary>
        private void CreateSensorUIItem(VirtualSensor sensor)
        {
            GameObject itemObj = Instantiate(sensorItemPrefab, itemContainer);
            itemObj.SetActive(true);
            SensorUIItem uiItem = itemObj.GetComponent<SensorUIItem>();
            
            if (uiItem == null)
            {
                uiItem = itemObj.AddComponent<SensorUIItem>();
            }
            
            uiItem.Initialize(sensor);
            uiItem.SetRemoveHandler(sensorInstance =>
            {
                if (sensorInstance != null)
                {
                    TryRemoveSensor(sensorInstance.name);
                }
            });
            sensorUIItems[sensor.name] = uiItem;
            
            if (showDebugInfo)
            {
                Debug.Log($"[SensorMonitorManager] ➕ Created UI item for sensor: {sensor.name}");
            }
        }

        /// <summary>
        /// 更新所有传感器的数据显示
        /// </summary>
        private void UpdateAllSensorData()
        {
            foreach (var kvp in sensorUIItems)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.UpdateDisplay();
                }
            }

            foreach (var kvp in avatarUIItems)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.UpdateDisplay();
                }
            }
        }

        /// <summary>
        /// 切换面板显示/隐藏
        /// </summary>
        public void TogglePanel()
        {
            isPanelVisible = !isPanelVisible;
            if (monitorPanel != null)
            {
                monitorPanel.SetActive(isPanelVisible);
            }
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public void ShowPanel()
        {
            isPanelVisible = true;
            if (monitorPanel != null)
            {
                monitorPanel.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel()
        {
            isPanelVisible = false;
            if (monitorPanel != null)
            {
                monitorPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 清空所有UI
        /// </summary>
        public void ClearAllUI()
        {
            foreach (var kvp in sensorUIItems)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            sensorUIItems.Clear();

            foreach (var kvp in avatarUIItems)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            avatarUIItems.Clear();
            
            if (showDebugInfo)
                Debug.Log("[SensorMonitorManager] 🗑️ Cleared all sensor UI items");
        }

        private void BindRecordingUi()
        {
            if (recordingButton == null)
            {
                return;
            }

            recordingButton.onClick.RemoveListener(OnRecordingButtonClicked);
            recordingButton.onClick.AddListener(OnRecordingButtonClicked);
        }

        private void OnRecordingButtonClicked()
        {
            if (sensorManager == null)
            {
                Debug.LogWarning("[SensorMonitorManager] ⚠️ Cannot toggle recording: sensor manager missing.");
                return;
            }
            
            if (!sensorManager.IsRecording)
            {
                sensorManager.StartSensorRecording();
                RefreshRecordingUi();
                return;
            }
            
            var result = sensorManager.StopSensorRecordingAndExport();
            if (result.saved)
            {
                Debug.Log($"[SensorMonitorManager] 💾 Sensor recording saved to {result.directoryPath}");
            }
            else if (result.canceled)
            {
                Debug.Log("[SensorMonitorManager] ℹ️ Sensor recording export canceled.");
            }
            
            RefreshRecordingUi();
        }

        private void RefreshRecordingUi()
        {
            if (recordingButtonImage != null)
            {
                recordingButtonImage.color = sensorManager != null && sensorManager.IsRecording
                    ? recordingButtonRecordingColor
                    : recordingButtonIdleColor;
            }
            
            if (recordingTimerText == null)
            {
                return;
            }
            
            bool isRecording = sensorManager != null && sensorManager.IsRecording;
            if (recordingTimerText.gameObject.activeSelf != isRecording)
            {
                recordingTimerText.gameObject.SetActive(isRecording);
            }
            
            if (isRecording)
            {
                recordingTimerText.text = FormatDuration(sensorManager.RecordingDurationSeconds);
            }
        }

        private static string FormatDuration(float seconds)
        {
            var duration = System.TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return $"{duration.Minutes:00}:{duration.Seconds:00}";
        }

        private void RefreshAvatarList()
        {
            if (itemContainer == null || avatarItemPrefab == null)
            {
                return;
            }

            avatarRuntimeManager ??= ServiceLocator.IsRegistered<AvatarRuntimeManager>()
                ? ServiceLocator.Get<AvatarRuntimeManager>()
                : FindFirstObjectByType<AvatarRuntimeManager>();
            sceneRegistry ??= ServiceLocator.IsRegistered<SceneRegistry>()
                ? ServiceLocator.Get<SceneRegistry>()
                : FindFirstObjectByType<SceneRegistry>();

            var avatars = avatarRuntimeManager != null
                ? avatarRuntimeManager.GetAvatarQueryModels(sceneRegistry)
                : new List<AvatarQueryModel>();

            var currentAvatarIds = new HashSet<string>();
            foreach (var avatar in avatars)
            {
                if (avatar == null || string.IsNullOrWhiteSpace(avatar.avatar_id))
                {
                    continue;
                }

                currentAvatarIds.Add(avatar.avatar_id);
                if (!avatarUIItems.TryGetValue(avatar.avatar_id, out var uiItem) || uiItem == null)
                {
                    CreateAvatarUIItem(avatar);
                    continue;
                }

                uiItem.UpdateAvatarModel(avatar);
            }

            List<string> toRemove = new List<string>();
            foreach (var kvp in avatarUIItems)
            {
                if (!currentAvatarIds.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                    if (kvp.Value != null && kvp.Value.gameObject != null)
                    {
                        Destroy(kvp.Value.gameObject);
                    }
                }
            }

            foreach (var avatarId in toRemove)
            {
                avatarUIItems.Remove(avatarId);
            }
        }

        private void CreateAvatarUIItem(AvatarQueryModel avatar)
        {
            GameObject itemObj = Instantiate(avatarItemPrefab, itemContainer);
            itemObj.SetActive(true);
            AvatarUIItem uiItem = itemObj.GetComponent<AvatarUIItem>();

            if (uiItem == null)
            {
                uiItem = itemObj.AddComponent<AvatarUIItem>();
            }

            uiItem.Initialize(avatar);
            uiItem.SetRemoveHandler(avatarId => TryRemoveAvatar(avatarId));
            avatarUIItems[avatar.avatar_id] = uiItem;

            if (showDebugInfo)
            {
                Debug.Log($"[SensorMonitorManager] 👤 Created UI item for avatar: {avatar.avatar_id}");
            }
        }

        public bool TryRemoveSensor(string sensorName)
        {
            if (string.IsNullOrWhiteSpace(sensorName))
            {
                return false;
            }

            sensorManager ??= VsensAgentSensorManager.Instance
                ?? (ServiceLocator.IsRegistered<VsensAgentSensorManager>()
                    ? ServiceLocator.Get<VsensAgentSensorManager>()
                    : FindFirstObjectByType<VsensAgentSensorManager>());

            bool removed;
            string error = null;
            if (sensorManager != null)
            {
                removed = sensorManager.TryRemoveSensor(sensorName, out error);
            }
            else
            {
                var sensor = FindObjectsByType<VirtualSensor>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate != null && candidate.name == sensorName);
                removed = sensor != null;
                if (removed)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(sensor.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(sensor.gameObject);
                    }
                }
                else
                {
                    error = $"Sensor '{sensorName}' not found.";
                }
            }

            if (!removed)
            {
                Debug.LogWarning($"[SensorMonitorManager] ⚠️ Failed to remove sensor '{sensorName}': {error}");
                return false;
            }

            ClearEditSelection(sensorName);
            RegisterMutation(sensorName, "remove_sensor");
            RemoveSensorUiItem(sensorName);
            RefreshSensorList();
            return true;
        }

        public bool TryRemoveAvatar(string avatarId)
        {
            avatarId = string.IsNullOrWhiteSpace(avatarId) ? "avatar_main" : avatarId;
            avatarRuntimeManager ??= ServiceLocator.IsRegistered<AvatarRuntimeManager>()
                ? ServiceLocator.Get<AvatarRuntimeManager>()
                : FindFirstObjectByType<AvatarRuntimeManager>();
            if (avatarRuntimeManager == null)
            {
                Debug.LogWarning("[SensorMonitorManager] ⚠️ Cannot remove avatar: AvatarRuntimeManager not found.");
                return false;
            }

            if (!avatarRuntimeManager.TryRemoveAvatar(avatarId, out var error))
            {
                Debug.LogWarning($"[SensorMonitorManager] ⚠️ Failed to remove avatar '{avatarId}': {error}");
                return false;
            }

            ClearEditSelection(avatarId);
            RegisterMutation(avatarId, "remove_avatar");
            RemoveAvatarUiItem(avatarId);
            RefreshSensorList();
            return true;
        }

        private void RemoveSensorUiItem(string sensorName)
        {
            if (!sensorUIItems.TryGetValue(sensorName, out var uiItem))
            {
                return;
            }

            if (uiItem != null && uiItem.gameObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(uiItem.gameObject);
                }
                else
                {
                    DestroyImmediate(uiItem.gameObject);
                }
            }

            sensorUIItems.Remove(sensorName);
        }

        private void RemoveAvatarUiItem(string avatarId)
        {
            if (!avatarUIItems.TryGetValue(avatarId, out var uiItem))
            {
                return;
            }

            if (uiItem != null && uiItem.gameObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(uiItem.gameObject);
                }
                else
                {
                    DestroyImmediate(uiItem.gameObject);
                }
            }

            avatarUIItems.Remove(avatarId);
        }

        private void ClearEditSelection(string objectId)
        {
            var editController = ServiceLocator.Get<RuntimeEditModeController>() ?? FindFirstObjectByType<RuntimeEditModeController>();
            editController?.ClearSelectionIfSelected(objectId);
        }

        private void RegisterMutation(string targetId, string actionType)
        {
            sceneRegistry ??= ServiceLocator.IsRegistered<SceneRegistry>()
                ? ServiceLocator.Get<SceneRegistry>()
                : FindFirstObjectByType<SceneRegistry>();
            sceneRegistry?.RegisterMutation("ui.monitor", targetId, actionType);
        }
    }
}
