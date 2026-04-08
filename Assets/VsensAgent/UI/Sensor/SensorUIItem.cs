using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Sensor;
using XCharts.Runtime;
using OVRSimpleJSON;
using VsensAgent.Core;
using VsensAgent.RuntimeEditing;

namespace VsensAgent.UI
{
    /// <summary>
    /// 单个传感器在UI列表中的Item组件
    /// </summary>
    public class SensorUIItem : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        public TextMeshProUGUI sensorNameText;      // 传感器名称
        public TextMeshProUGUI sensorTypeText;      // 传感器类型
        public TextMeshProUGUI sensorDataText;      // 传感器数据
        public Toggle detailToggle;            
        public Image statusIndicator;                // 状态指示器
        public Image selectionHighlight;             // 选中高亮背景
        
        public GameObject detailContainer;       
        
        public LineChart chart;
        public Button activeButton;
        public Button previewButton;
        public Button graphButton;
        
        [Header("Display Settings")]
        public Color activeColor = Color.green;      // 活动状态颜色
        public Color inactiveColor = Color.gray;     // 非活动状态颜色
        public Color selectedHighlightColor = Color.red;
        public Color unselectedHighlightColor = new Color(1f, 0f, 0f, 0f);
        
        // 私有变量
        private VirtualSensor sensor;
        private bool isInitialized = false;
        private string lastDataString = "";
        private readonly List<Tuple<float, float[]>> chartSamples = new();
        private const int MaxChartSamples = 60;
        private int chartDimension = -1;
        private RuntimeEditModeController cachedEditController;

        /// <summary>
        /// 初始化UI Item，绑定传感器
        /// </summary>
        public void Initialize(VirtualSensor sensorInstance)
        {
            UnbindSensorEvents();
            sensor = sensorInstance;
            isInitialized = true;
            BindSensorEvents();

            if (activeButton != null)
            {
                activeButton.onClick.RemoveAllListeners();
                activeButton.onClick.AddListener(OnActiveButtonClicked);
            }

            if (previewButton != null)
            {
                previewButton.onClick.RemoveAllListeners();
                previewButton.onClick.AddListener(OnPreviewButtonClicked);
            }

            if (graphButton != null)
            {
                graphButton.onClick.RemoveAllListeners();
                graphButton.onClick.AddListener(OnGraphButtonClicked);
            }
            
            UpdateBasicInfo();
            UpdateDisplay();
            UpdateSelectionHighlight();
        }

        /// <summary>
        /// 更新传感器引用（当传感器重新创建时）
        /// </summary>
        public void UpdateSensorReference(VirtualSensor newSensor)
        {
            if (ReferenceEquals(sensor, newSensor))
            {
                return;
            }

            UnbindSensorEvents();
            sensor = newSensor;
            BindSensorEvents();
            UpdateBasicInfo();
            RebuildChartFromHistory();
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
                statusIndicator.color = sensor.IsActive ? activeColor : inactiveColor;
            }
                        
            if (activeButton != null)
            {
                activeButton.GetComponent<Image>().color = sensor.IsActive ? activeColor : inactiveColor;
            }

            if (previewButton != null)
            {
                previewButton.GetComponent<Image>().color = sensor.ShowPreview ? activeColor : inactiveColor;
            }

            if (graphButton != null)
            {
                graphButton.GetComponent<Image>().color = sensor.ShowGraph ? activeColor : inactiveColor;
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
                    
                    if (detailToggle.isOn)
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

        private void OnActiveButtonClicked()
        {
            if (sensor == null) return;
            sensor.IsActive = !sensor.IsActive;
        }
        
        private void OnPreviewButtonClicked()
        {
            if (sensor == null) return;
            sensor.ShowPreview = !sensor.ShowPreview;
        }
        
        private void OnGraphButtonClicked()
        {
            if (sensor == null) return;
            sensor.ShowGraph = !sensor.ShowGraph;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || sensor == null)
            {
                return;
            }

            if (IsControlClick(eventData.pointerPressRaycast.gameObject) || IsControlClick(eventData.pointerCurrentRaycast.gameObject))
            {
                return;
            }

            TrySelectSensorForEditing();
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

            if (selectionHighlight != null)
            {
                selectionHighlight.color = unselectedHighlightColor;
            }
            
            if (detailToggle != null && detailContainer != null)
            {
                detailToggle.isOn = detailContainer.activeSelf;
                detailToggle.onValueChanged.RemoveAllListeners();
                detailToggle.onValueChanged.AddListener(OnDetailToggleValueChanged);
            }
        }

        private void OnEnable()
        {
            TrySubscribeSelectionEvents();
            UpdateSelectionHighlight();
        }

        private void OnDisable()
        {
            UnsubscribeSelectionEvents();
        }
        
        private void OnDetailToggleValueChanged(bool isOn)
        {
            if (detailContainer != null)
            {
                detailContainer.SetActive(isOn);
            }

            if (isOn)
            {
                RebuildChartFromHistory();
            }
        }

        private void BindSensorEvents()
        {
            if (sensor == null)
            {
                return;
            }

            sensor.onDataAppended += OnSensorDataAppended;
        }

        private void UnbindSensorEvents()
        {
            if (sensor == null)
            {
                return;
            }

            sensor.onDataAppended -= OnSensorDataAppended;
        }

        private void OnDestroy()
        {
            UnbindSensorEvents();
            UnsubscribeSelectionEvents();
        }

        private bool IsControlClick(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            return (detailToggle != null && target.transform.IsChildOf(detailToggle.transform)) ||
                   (activeButton != null && target.transform.IsChildOf(activeButton.transform)) ||
                   (previewButton != null && target.transform.IsChildOf(previewButton.transform)) ||
                   (graphButton != null && target.transform.IsChildOf(graphButton.transform));
        }

        private void OnItemSelectedFromButton()
        {
            TrySelectSensorForEditing();
        }

        private void TrySelectSensorForEditing()
        {
            var editController = GetEditController();
            if (editController == null || !editController.IsEditModeEnabled || sensor == null)
            {
                return;
            }

            editController.TrySelectEditableFromUi(sensor.name);
            UpdateSelectionHighlight();
        }

        private RuntimeEditModeController GetEditController()
        {
            if (cachedEditController != null)
            {
                return cachedEditController;
            }

            cachedEditController = ServiceLocator.Get<RuntimeEditModeController>() ?? FindFirstObjectByType<RuntimeEditModeController>();
            return cachedEditController;
        }

        private void TrySubscribeSelectionEvents()
        {
            var editController = GetEditController();
            if (editController == null)
            {
                return;
            }

            editController.SelectionChanged -= OnSelectionChanged;
            editController.SelectionChanged += OnSelectionChanged;
        }

        private void UnsubscribeSelectionEvents()
        {
            if (cachedEditController == null)
            {
                return;
            }

            cachedEditController.SelectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged(string selectedObjectId)
        {
            UpdateSelectionHighlight();
        }

        private void UpdateSelectionHighlight()
        {
            if (selectionHighlight == null)
            {
                return;
            }

            var editController = GetEditController();
            var isSelected = sensor != null &&
                             editController != null &&
                             editController.IsEditModeEnabled &&
                             string.Equals(editController.SelectedObjectId, sensor.name, StringComparison.Ordinal);

            selectionHighlight.color = isSelected ? selectedHighlightColor : unselectedHighlightColor;
        }

        private void OnSensorDataAppended(SensorData sample, bool isRecording)
        {
            if (!isInitialized)
            {
                return;
            }

            if (sensorDataText != null)
            {
                lastDataString = "";
            }

            if (detailToggle == null || !detailToggle.isOn || chart == null)
            {
                return;
            }

            var values = ExtractChartValues(sample);
            if (values == null || values.Length == 0)
            {
                return;
            }

            chartSamples.Add(Tuple.Create(sample.time, values));
            while (chartSamples.Count > MaxChartSamples)
            {
                chartSamples.RemoveAt(0);
            }

            EnsureChartDimension(values.Length);
            RedrawChart();
        }

        private void RebuildChartFromHistory()
        {
            chartSamples.Clear();
            chartDimension = -1;

            if (sensor == null || chart == null)
            {
                return;
            }

            var startIndex = Mathf.Max(0, sensor.Data.Count - MaxChartSamples);
            for (var i = startIndex; i < sensor.Data.Count; i++)
            {
                var sample = sensor.Data[i];
                var values = ExtractChartValues(sample);
                if (values == null || values.Length == 0)
                {
                    continue;
                }

                chartSamples.Add(Tuple.Create(sample.time, values));
            }

            var expectedDimension = chartSamples.Count > 0 ? chartSamples[chartSamples.Count - 1].Item2.Length : 1;
            EnsureChartDimension(expectedDimension);
            RedrawChart();
        }

        private void RedrawChart()
        {
            if (chart == null)
            {
                return;
            }

            chart.ClearData();
            foreach (var sample in chartSamples)
            {
                chart.AddXAxisData(sample.Item1.ToString("F2"));
                for (var i = 0; i < sample.Item2.Length; i++)
                {
                    chart.AddData(i, sample.Item2[i]);
                }
            }
        }

        private float[] ExtractChartValues(SensorData sample)
        {
            if (sample.data == null)
            {
                return Array.Empty<float>();
            }

            JSONNode payload;
            try
            {
                payload = sample.data.serialize();
            }
            catch
            {
                return Array.Empty<float>();
            }

            if (payload == null)
            {
                return Array.Empty<float>();
            }

            if (sensor is VirtualIMUSensor)
            {
                return ReadVectorComponents(payload["localAcceleration"]);
            }

            if (sensor is VirtualDistanceSensor)
            {
                return new[] { payload["distance"]?.AsFloat ?? 0f };
            }

            if (sensor is VirtualLightSensor)
            {
                return new[] { payload["lux"]?.AsFloat ?? 0f };
            }

            if (payload["value"] != null)
            {
                return new[] { payload["value"].AsFloat };
            }

            return Array.Empty<float>();
        }

        private static float[] ReadVectorComponents(JSONNode node)
        {
            if (node == null)
            {
                return new[] { 0f, 0f, 0f };
            }

            return new[]
            {
                node["x"]?.AsFloat ?? 0f,
                node["y"]?.AsFloat ?? 0f,
                node["z"]?.AsFloat ?? 0f
            };
        }

        private void EnsureChartDimension(int expectedCount)
        {
            if (chart == null)
            {
                return;
            }

            var targetCount = Mathf.Max(1, expectedCount);
            if (chartDimension == targetCount && chart.series.Count == targetCount)
            {
                return;
            }

            while (chart.series.Count > targetCount)
            {
                chart.RemoveSerie(chart.series.Count - 1);
            }

            while (chart.series.Count < targetCount)
            {
                var serie = chart.AddSerie<Line>($"Series {chart.series.Count + 1}");
                serie.animation.enable = false;
                if (serie?.symbol != null)
                {
                    serie.symbol.show = false;
                }
            }

            chartDimension = targetCount;
        }
    }
}
