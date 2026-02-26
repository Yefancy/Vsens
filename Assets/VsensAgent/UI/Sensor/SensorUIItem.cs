using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sensor;

namespace VsensAgent.UI
{
    /// <summary>
    /// 单个传感器在UI列表中的Item组件
    /// </summary>
    public class SensorUIItem : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI sensorNameText;      // 传感器名称
        public TextMeshProUGUI sensorTypeText;      // 传感器类型
        public TextMeshProUGUI sensorDataText;      // 传感器数据
        public Image statusIndicator;                // 状态指示器
        public Button selectButton;                  // 选择按钮（可选）
        
        [Header("Display Settings")]
        public Color activeColor = Color.green;      // 活动状态颜色
        public Color inactiveColor = Color.gray;     // 非活动状态颜色
        public bool showDetailedData = true;         // 显示详细数据
        
        // 私有变量
        private VirtualSensor sensor;
        private bool isInitialized = false;
        private string lastDataString = "";

        /// <summary>
        /// 初始化UI Item，绑定传感器
        /// </summary>
        public void Initialize(VirtualSensor sensorInstance)
        {
            sensor = sensorInstance;
            isInitialized = true;
            
            UpdateBasicInfo();
            UpdateDisplay();
            
            // 如果有选择按钮，添加点击事件
            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectButtonClicked);
            }
        }

        /// <summary>
        /// 更新传感器引用（当传感器重新创建时）
        /// </summary>
        public void UpdateSensorReference(VirtualSensor newSensor)
        {
            sensor = newSensor;
            UpdateBasicInfo();
        }

        /// <summary>
        /// 更新基本信息（名称、类型等）
        /// </summary>
        private void UpdateBasicInfo()
        {
            if (sensor == null) return;
            
            // 更新名称
            if (sensorNameText != null)
            {
                sensorNameText.text = sensor.name;
            }
            
            // 更新类型
            if (sensorTypeText != null)
            {
                try
                {
                    var definition = sensor.SensorDefinition();
                    sensorTypeText.text = definition?.getSensorName() ?? "Unknown";
                }
                catch
                {
                    sensorTypeText.text = "Unknown";
                }
            }
        }

        /// <summary>
        /// 更新显示（每帧或定时调用）
        /// </summary>
        public void UpdateDisplay()
        {
            if (!isInitialized || sensor == null)
            {
                SetInactiveState();
                return;
            }
            
            // 检查传感器是否仍然存在
            if (sensor.gameObject == null)
            {
                SetInactiveState();
                return;
            }
            
            // 更新状态指示器
            if (statusIndicator != null)
            {
                statusIndicator.color = sensor.isActiveAndEnabled ? activeColor : inactiveColor;
            }
            
            // 更新传感器数据显示
            if (sensorDataText != null)
            {
                string dataString = GetSensorDataString();
                if (dataString != lastDataString)
                {
                    sensorDataText.text = dataString;
                    lastDataString = dataString;
                }
            }
        }

        /// <summary>
        /// 设置为非活动状态
        /// </summary>
        private void SetInactiveState()
        {
            if (sensorDataText != null)
            {
                sensorDataText.text = "Inactive";
                sensorDataText.color = Color.gray;
            }
            
            if (statusIndicator != null)
            {
                statusIndicator.color = inactiveColor;
            }
        }

        /// <summary>
        /// 获取传感器数据的字符串表示
        /// </summary>
        private string GetSensorDataString()
        {
            if (sensor == null) return "No Data";
            
            try
            {
                // 尝试获取传感器描述
                if (sensor is VirtualSensor virtualSensor)
                {
                    string description = virtualSensor.GetSensorDescription();
                    
                    if (showDetailedData)
                    {
                        return description;
                    }
                    else
                    {
                        // 简化显示：只显示关键信息
                        return GetSimplifiedDataString(description);
                    }
                }
                
                return "Active";
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SensorUIItem] ⚠️ Failed to get data for {sensor.name}: {ex.Message}");
                return "Error";
            }
        }

        /// <summary>
        /// 获取简化的数据字符串
        /// </summary>
        private string GetSimplifiedDataString(string fullDescription)
        {
            // 这里可以根据需要解析和简化描述
            // 例如：只显示第一行，或提取关键数值
            if (string.IsNullOrEmpty(fullDescription))
                return "Active";
            
            // 取第一行或前50个字符
            int newlineIndex = fullDescription.IndexOf('\n');
            if (newlineIndex > 0)
            {
                return fullDescription.Substring(0, newlineIndex);
            }
            
            if (fullDescription.Length > 50)
            {
                return fullDescription.Substring(0, 47) + "...";
            }
            
            return fullDescription;
        }

        /// <summary>
        /// 选择按钮点击事件
        /// </summary>
        private void OnSelectButtonClicked()
        {
            if (sensor == null) return;
            
            Debug.Log($"[SensorUIItem] 🎯 Selected sensor: {sensor.name}");
            
            // 可以在这里添加选择传感器的逻辑
            // 例如：在场景中高亮显示传感器，或显示详细信息面板
            HighlightSensorInScene();
        }

        /// <summary>
        /// 在场景中高亮显示传感器
        /// </summary>
        private void HighlightSensorInScene()
        {
            if (sensor == null || sensor.gameObject == null) return;
            
            // 可以添加视觉效果，例如：
            // - 改变传感器颜色
            // - 添加轮廓效果
            // - 移动相机聚焦到传感器
            
            // 这里只是一个示例：让相机看向传感器
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Debug.Log($"[SensorUIItem] 📷 Camera looking at sensor: {sensor.name} at position {sensor.transform.position}");
            }
        }

        /// <summary>
        /// 获取传感器实例
        /// </summary>
        public VirtualSensor GetSensor()
        {
            return sensor;
        }

        /// <summary>
        /// 检查是否已初始化
        /// </summary>
        public bool IsInitialized()
        {
            return isInitialized && sensor != null;
        }

        /// <summary>
        /// 自动查找子物体中的UI组件
        /// </summary>
        void Awake()
        {
            // 如果引用未设置，尝试自动查找
            if (sensorNameText == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>();
                if (texts.Length > 0) sensorNameText = texts[0];
                if (texts.Length > 1) sensorTypeText = texts[1];
                if (texts.Length > 2) sensorDataText = texts[2];
            }
            
            if (statusIndicator == null)
            {
                statusIndicator = GetComponentInChildren<Image>();
            }
            
            if (selectButton == null)
            {
                selectButton = GetComponentInChildren<Button>();
            }
        }
    }
}
