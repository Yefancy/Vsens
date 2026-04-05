using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VsensAgent.VirtualObject.Sensor;
using Sensor;

namespace VsensAgent.UI
{
    /// <summary>
    /// 管理传感器监控面板UI，显示场景中所有传感器的列表和数据
    /// </summary>
    public class SensorMonitorManager : MonoBehaviour
    {
        [Header("UI Panel References")]
        public GameObject sensorMonitorPanel;           // 整个传感器监控面板
        public ScrollRect sensorListScrollRect;         // 传感器列表滚动区域
        public Transform sensorItemContainer;           // 传感器Item的容器 (ScrollRect的Content)
        
        [Header("Prefabs")]
        public GameObject sensorItemPrefab;             // 传感器Item预制件
        
        [Header("Settings")]
        public float updateInterval = 0.5f;             // 数据更新间隔（秒）
        public bool autoRefreshList = true;             // 自动刷新传感器列表
        public float listRefreshInterval = 2f;          // 列表刷新间隔（秒）
        
        [Header("Debug")]
        public bool showDebugInfo = false;               // 显示调试信息
        
        // 私有变量
        private VsensAgentSensorManager sensorManager;
        private Dictionary<string, SensorUIItem> sensorUIItems = new Dictionary<string, SensorUIItem>();
        private float nextUpdateTime;
        private float nextRefreshTime;
        private bool isPanelVisible = true;

        void Awake()
        {
            // 确保组件引用
            if (sensorListScrollRect == null)
                sensorListScrollRect = GetComponentInChildren<ScrollRect>();
            
            if (sensorItemContainer == null && sensorListScrollRect != null)
                sensorItemContainer = sensorListScrollRect.content;
        }

        void Start()
        {
            // 获取传感器管理器实例
            sensorManager = VsensAgentSensorManager.Instance;
            
            if (sensorManager == null)
            {
                Debug.LogError("[SensorMonitorManager] ❌ VsensAgentSensorManager instance not found!");
                return;
            }
            
            if (sensorMonitorPanel != null)
            {
                sensorMonitorPanel.SetActive(isPanelVisible);
            }
            
            // 初始化传感器列表
            RefreshSensorList();
            
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
        }

        /// <summary>
        /// 刷新传感器列表 - 重新获取所有传感器并更新UI
        /// </summary>
        public void RefreshSensorList()
        {
            if (sensorManager == null || sensorItemContainer == null)
            {
                Debug.LogWarning("[SensorMonitorManager] ⚠️ Cannot refresh sensor list - missing references");
                return;
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
        }

        /// <summary>
        /// 为传感器创建UI Item
        /// </summary>
        private void CreateSensorUIItem(VirtualSensor sensor)
        {
            if (sensorItemPrefab == null)
            {
                // 如果没有预制件，动态创建一个简单的UI Item
                CreateSimpleSensorUIItem(sensor);
                return;
            }
            
            GameObject itemObj = Instantiate(sensorItemPrefab, sensorItemContainer);
            itemObj.SetActive(true);
            SensorUIItem uiItem = itemObj.GetComponent<SensorUIItem>();
            
            if (uiItem == null)
            {
                uiItem = itemObj.AddComponent<SensorUIItem>();
            }
            
            uiItem.Initialize(sensor);
            sensorUIItems[sensor.name] = uiItem;
            
            if (showDebugInfo)
            {
                Debug.Log($"[SensorMonitorManager] ➕ Created UI item for sensor: {sensor.name}");
            }
        }

        /// <summary>
        /// 创建简单的传感器UI Item（当没有预制件时）
        /// </summary>
        private void CreateSimpleSensorUIItem(VirtualSensor sensor)
        {
            // 创建一个简单的UI元素
            GameObject itemObj = new GameObject($"SensorItem_{sensor.name}");
            itemObj.transform.SetParent(sensorItemContainer, false);
            
            // 添加布局组件
            RectTransform rt = itemObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 60); // 高度60
            
            // 添加背景
            Image bg = itemObj.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // 添加水平布局
            HorizontalLayoutGroup layout = itemObj.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            
            // 添加文本显示传感器名称和类型
            CreateTextElement(itemObj.transform, "Name", sensor.name, 200);
            CreateTextElement(itemObj.transform, "Type", GetSensorTypeName(sensor), 150);
            CreateTextElement(itemObj.transform, "Data", "Initializing...", 200);
            
            // 添加SensorUIItem组件
            SensorUIItem uiItem = itemObj.AddComponent<SensorUIItem>();
            uiItem.Initialize(sensor);
            
            sensorUIItems[sensor.name] = uiItem;
            
            if (showDebugInfo)
            {
                Debug.Log($"[SensorMonitorManager] ➕ Created simple UI item for sensor: {sensor.name}");
            }
        }

        /// <summary>
        /// 创建文本元素辅助方法
        /// </summary>
        private GameObject CreateTextElement(Transform parent, string name, string text, float width)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            
            RectTransform rt = textObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 0);
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            
            LayoutElement le = textObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = 0;
            
            return textObj;
        }

        /// <summary>
        /// 获取传感器类型名称
        /// </summary>
        private string GetSensorTypeName(VirtualSensor sensor)
        {
            try
            {
                var definition = sensor.SensorDefinition();
                return definition?.getSensorName() ?? "Unknown";
            }
            catch
            {
                return "Unknown";
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
        }

        /// <summary>
        /// 切换面板显示/隐藏
        /// </summary>
        public void TogglePanel()
        {
            isPanelVisible = !isPanelVisible;
            if (sensorMonitorPanel != null)
            {
                sensorMonitorPanel.SetActive(isPanelVisible);
            }
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public void ShowPanel()
        {
            isPanelVisible = true;
            if (sensorMonitorPanel != null)
            {
                sensorMonitorPanel.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        public void HidePanel()
        {
            isPanelVisible = false;
            if (sensorMonitorPanel != null)
            {
                sensorMonitorPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 清空所有传感器UI
        /// </summary>
        public void ClearAllSensorUI()
        {
            foreach (var kvp in sensorUIItems)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            sensorUIItems.Clear();
            
            if (showDebugInfo)
                Debug.Log("[SensorMonitorManager] 🗑️ Cleared all sensor UI items");
        }
    }
}
