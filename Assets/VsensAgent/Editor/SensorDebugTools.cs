using UnityEngine;
using UnityEditor;
using Sensor;
using System.Collections.Generic;

namespace VsensAgent.Editor
{
    /// <summary>
    /// Unity Editor工具，用于在场景中快速添加和测试VirtualSensor
    /// 用于测试SensorDataCenter和SensorMonitorPanel
    /// </summary>
    public class SensorDebugTools : EditorWindow
    {
        [MenuItem("VsensAgent/Debug Tools/Sensor Debug Tools")]
        public static void ShowWindow()
        {
            GetWindow<SensorDebugTools>("Sensor Debug Tools");
        }

        // 传感器预制体路径（如果有预制体）
        private GameObject imuPrefab;
        private GameObject lightPrefab;
        private GameObject distancePrefab;
        
        // 创建选项
        private Vector3 spawnPosition = Vector3.zero;
        private bool autoRegister = true;
        private bool setActive = true;
        private int batchCount = 1;
        private float batchSpacing = 1.0f;
        private SensorType selectedSensorType = SensorType.IMU;
        
        // UI状态
        private Vector2 scrollPosition;
        private bool showAdvancedOptions = false;
        
        enum SensorType
        {
            IMU,
            Light,
            Distance
        }

        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            // 标题
            GUILayout.Label("🔧 传感器调试工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);
            
            EditorGUILayout.HelpBox("用于快速在场景中添加VirtualSensor进行测试", MessageType.Info);
            EditorGUILayout.Space(10);

            // ===== 传感器类型选择 =====
            GUILayout.Label("传感器类型", EditorStyles.boldLabel);
            selectedSensorType = (SensorType)EditorGUILayout.EnumPopup("类型", selectedSensorType);
            
            // 显示对应的预制体引用（可选）
            switch (selectedSensorType)
            {
                case SensorType.IMU:
                    imuPrefab = (GameObject)EditorGUILayout.ObjectField("IMU 预制体 (可选)", imuPrefab, typeof(GameObject), false);
                    break;
                case SensorType.Light:
                    lightPrefab = (GameObject)EditorGUILayout.ObjectField("Light 预制体 (可选)", lightPrefab, typeof(GameObject), false);
                    break;
                case SensorType.Distance:
                    distancePrefab = (GameObject)EditorGUILayout.ObjectField("Distance 预制体 (可选)", distancePrefab, typeof(GameObject), false);
                    break;
            }
            
            EditorGUILayout.Space(10);

            // ===== 基本选项 =====
            GUILayout.Label("基本选项", EditorStyles.boldLabel);
            spawnPosition = EditorGUILayout.Vector3Field("生成位置", spawnPosition);
            autoRegister = EditorGUILayout.Toggle("自动注册到SensorDataCenter", autoRegister);
            setActive = EditorGUILayout.Toggle("激活传感器", setActive);
            
            EditorGUILayout.Space(10);

            // ===== 高级选项 =====
            showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "高级选项", true);
            if (showAdvancedOptions)
            {
                EditorGUI.indentLevel++;
                batchCount = EditorGUILayout.IntSlider("批量创建数量", batchCount, 1, 20);
                if (batchCount > 1)
                {
                    batchSpacing = EditorGUILayout.Slider("间距", batchSpacing, 0.1f, 5.0f);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(15);

            // ===== 操作按钮 =====
            GUILayout.Label("操作", EditorStyles.boldLabel);
            
            // 创建新GameObject并添加传感器
            if (GUILayout.Button("🆕 创建新传感器GameObject", GUILayout.Height(35)))
            {
                CreateNewSensor();
            }

            EditorGUILayout.Space(5);

            // 在选中的GameObject上添加传感器组件
            GUI.enabled = Selection.activeGameObject != null;
            if (GUILayout.Button("➕ 在选中对象上添加传感器组件", GUILayout.Height(35)))
            {
                AddSensorToSelected();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(5);

            // 使用预制体实例化（如果有）
            GameObject prefab = GetCurrentPrefab();
            GUI.enabled = prefab != null;
            if (GUILayout.Button("📦 从预制体实例化", GUILayout.Height(35)))
            {
                InstantiateFromPrefab();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(15);

            // ===== 快速测试功能 =====
            GUILayout.Label("快速测试", EditorStyles.boldLabel);
            
            if (GUILayout.Button("🚀 创建测试场景 (3种传感器各1个)", GUILayout.Height(35)))
            {
                CreateTestScenario();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("🔍 查找场景中所有传感器", GUILayout.Height(30)))
            {
                FindAllSensors();
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("🗑️ 删除场景中所有传感器", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("确认删除", 
                    "确定要删除场景中所有的VirtualSensor吗？", 
                    "删除", "取消"))
                {
                    DeleteAllSensors();
                }
            }

            EditorGUILayout.Space(15);

            // ===== 信息显示 =====
            GUILayout.Label("场景信息", EditorStyles.boldLabel);
            var sensorDataCenter = FindObjectOfType<SensorDataCenter>();
            if (sensorDataCenter != null)
            {
                EditorGUILayout.HelpBox($"✅ SensorDataCenter 已找到\n采样率: {sensorDataCenter.samplingRate} Hz", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠️ 场景中未找到 SensorDataCenter", MessageType.Warning);
            }

            var sensors = FindObjectsOfType<VirtualSensor>();
            EditorGUILayout.HelpBox($"场景中传感器数量: {sensors.Length}", MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private GameObject GetCurrentPrefab()
        {
            switch (selectedSensorType)
            {
                case SensorType.IMU:
                    return imuPrefab;
                case SensorType.Light:
                    return lightPrefab;
                case SensorType.Distance:
                    return distancePrefab;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 创建新的GameObject并添加对应的传感器组件
        /// </summary>
        private void CreateNewSensor()
        {
            for (int i = 0; i < batchCount; i++)
            {
                Vector3 position = spawnPosition + new Vector3(i * batchSpacing, 0, 0);
                GameObject sensorObj = new GameObject(GetSensorName() + (batchCount > 1 ? $"_{i + 1}" : ""));
                sensorObj.transform.position = position;

                VirtualSensor sensor = AddSensorComponent(sensorObj);
                ConfigureSensor(sensor);

                Undo.RegisterCreatedObjectUndo(sensorObj, "Create " + GetSensorName());
                Selection.activeGameObject = sensorObj;
                
                Debug.Log($"✅ 创建传感器: {sensorObj.name} at {position}");
            }
        }

        /// <summary>
        /// 在选中的GameObject上添加传感器组件
        /// </summary>
        private void AddSensorToSelected()
        {
            GameObject selectedObj = Selection.activeGameObject;
            if (selectedObj == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择一个GameObject", "确定");
                return;
            }

            VirtualSensor sensor = AddSensorComponent(selectedObj);
            ConfigureSensor(sensor);

            Undo.RegisterCompleteObjectUndo(selectedObj, "Add Sensor Component");
            Debug.Log($"✅ 在 {selectedObj.name} 上添加了 {GetSensorName()} 组件");
        }

        /// <summary>
        /// 从预制体实例化传感器
        /// </summary>
        private void InstantiateFromPrefab()
        {
            GameObject prefab = GetCurrentPrefab();
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("错误", "未设置预制体", "确定");
                return;
            }

            for (int i = 0; i < batchCount; i++)
            {
                Vector3 position = spawnPosition + new Vector3(i * batchSpacing, 0, 0);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.position = position;
                
                if (batchCount > 1)
                {
                    instance.name = $"{prefab.name}_{i + 1}";
                }

                VirtualSensor sensor = instance.GetComponent<VirtualSensor>();
                if (sensor != null)
                {
                    ConfigureSensor(sensor);
                }

                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Sensor Prefab");
                Selection.activeGameObject = instance;
                
                Debug.Log($"✅ 实例化传感器预制体: {instance.name} at {position}");
            }
        }

        /// <summary>
        /// 根据选择的类型添加对应的传感器组件
        /// </summary>
        private VirtualSensor AddSensorComponent(GameObject obj)
        {
            switch (selectedSensorType)
            {
                case SensorType.IMU:
                    return obj.AddComponent<VirtualIMUSensor>();
                case SensorType.Light:
                    return obj.AddComponent<VirtualLightSensor>();
                case SensorType.Distance:
                    return obj.AddComponent<VirtualDistanceSensor>();
                default:
                    return null;
            }
        }

        /// <summary>
        /// 配置传感器的基本属性
        /// </summary>
        private void ConfigureSensor(VirtualSensor sensor)
        {
            if (sensor == null) return;

            sensor.registerOnStart = autoRegister;
            sensor.IsActive = setActive;
            
            // 如果SensorDataCenter存在且需要自动注册，立即注册
            if (autoRegister)
            {
                var sensorDataCenter = FindObjectOfType<SensorDataCenter>();
                if (sensorDataCenter != null && Application.isPlaying)
                {
                    sensorDataCenter.RegisterSensor(sensor);
                }
            }
        }

        /// <summary>
        /// 创建测试场景：创建3种传感器各1个
        /// </summary>
        private void CreateTestScenario()
        {
            Vector3 basePosition = spawnPosition;
            
            // 创建IMU传感器
            GameObject imuObj = new GameObject("TestSensor_IMU");
            imuObj.transform.position = basePosition;
            VirtualIMUSensor imu = imuObj.AddComponent<VirtualIMUSensor>();
            ConfigureSensor(imu);
            Undo.RegisterCreatedObjectUndo(imuObj, "Create Test Scenario");

            // 创建Light传感器
            GameObject lightObj = new GameObject("TestSensor_Light");
            lightObj.transform.position = basePosition + new Vector3(1.5f, 0, 0);
            VirtualLightSensor light = lightObj.AddComponent<VirtualLightSensor>();
            ConfigureSensor(light);
            Undo.RegisterCreatedObjectUndo(lightObj, "Create Test Scenario");

            // 创建Distance传感器
            GameObject distanceObj = new GameObject("TestSensor_Distance");
            distanceObj.transform.position = basePosition + new Vector3(3.0f, 0, 0);
            VirtualDistanceSensor distance = distanceObj.AddComponent<VirtualDistanceSensor>();
            ConfigureSensor(distance);
            Undo.RegisterCreatedObjectUndo(distanceObj, "Create Test Scenario");

            Debug.Log("✅ 测试场景创建完成：已创建 IMU、Light、Distance 传感器各1个");
            
            // 选择第一个创建的对象
            Selection.activeGameObject = imuObj;
        }

        /// <summary>
        /// 查找场景中所有的传感器并在控制台显示
        /// </summary>
        private void FindAllSensors()
        {
            var allSensors = FindObjectsOfType<VirtualSensor>();
            Debug.Log($"=== 场景中共找到 {allSensors.Length} 个传感器 ===");
            
            var imuSensors = new List<VirtualIMUSensor>();
            var lightSensors = new List<VirtualLightSensor>();
            var distanceSensors = new List<VirtualDistanceSensor>();
            
            foreach (var sensor in allSensors)
            {
                if (sensor is VirtualIMUSensor imu)
                    imuSensors.Add(imu);
                else if (sensor is VirtualLightSensor light)
                    lightSensors.Add(light);
                else if (sensor is VirtualDistanceSensor distance)
                    distanceSensors.Add(distance);
                
                Debug.Log($"  - {sensor.GetType().Name}: {sensor.name} " +
                         $"(Active: {sensor.IsActive}, Position: {sensor.transform.position})");
            }
            
            Debug.Log($"\n统计: IMU={imuSensors.Count}, Light={lightSensors.Count}, Distance={distanceSensors.Count}");
        }

        /// <summary>
        /// 删除场景中所有的传感器
        /// </summary>
        private void DeleteAllSensors()
        {
            var allSensors = FindObjectsOfType<VirtualSensor>();
            int count = allSensors.Length;
            
            foreach (var sensor in allSensors)
            {
                Undo.DestroyObjectImmediate(sensor.gameObject);
            }
            
            Debug.Log($"🗑️ 已删除 {count} 个传感器");
        }

        /// <summary>
        /// 获取当前选择的传感器类型名称
        /// </summary>
        private string GetSensorName()
        {
            switch (selectedSensorType)
            {
                case SensorType.IMU:
                    return "IMU_Sensor";
                case SensorType.Light:
                    return "Light_Sensor";
                case SensorType.Distance:
                    return "Distance_Sensor";
                default:
                    return "Sensor";
            }
        }
    }
}
