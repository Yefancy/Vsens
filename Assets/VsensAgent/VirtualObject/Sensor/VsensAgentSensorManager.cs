using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sensor;
using UnityEngine;
using VsensAgent.Core;

namespace VsensAgent.VirtualObject.Sensor
{
    public class VsensAgentSensorManager : MonoBehaviour
    {
        public static VsensAgentSensorManager Instance { get; private set; }

        [SerializeField] private List<VirtualSensor> _registeredSensors = new List<VirtualSensor>();
        [Header("Test Settings")]
        [SerializeField] private Transform testParent; // Test parent for sensor creation

        private void Awake() 
        {
            if (Instance == null)
            {
                Instance = this;
                ServiceLocator.Register<VsensAgentSensorManager>(this);
                Debug.Log("[VsensAgentSensorManager] 🔧 Singleton instance initialized and registered to ServiceLocator");
            }
            else if (Instance != this)
            {
                Debug.LogWarning($"[VsensAgentSensorManager] ⚠️ Multiple instances detected. Destroying duplicate on {gameObject.name}");
                Destroy(this.gameObject);
            }
        }

        public VsensAgentSensorManager()
        {
            // 移除构造函数中的单例逻辑，改用Awake
        }

        public void RegisterSensor(VirtualSensor sensor)
        {
            if (!_registeredSensors.Contains(sensor))
            {
                _registeredSensors.Add(sensor);
            }
            else
            {
                Debug.LogWarning($"Sensor {sensor.name} is already registered.");
            }
        }

        public List<string> GetRegisteredSensorNames()
        {
            return _registeredSensors.Select(sensor => sensor.SensorDefinition().getSensorName()).ToList();
        }

        public bool HasSensor(string sensorName)
        {
            return _registeredSensors.Any(prefab => prefab.SensorDefinition().getSensorName() == sensorName);
        }

        [CanBeNull]
        public VirtualSensor CreateSensorByName(string sensorName, Transform parent)
        {
            Debug.Log($"[SensorManager] 🔍 Attempting to create sensor: '{sensorName}'");
            Debug.Log($"[SensorManager] 📋 Available sensors: {string.Join(", ", GetRegisteredSensorNames())}");
            
            foreach (var prefab in _registeredSensors)
            {
                string prefabSensorName = prefab.SensorDefinition().getSensorName();
                Debug.Log($"[SensorManager] 🔍 Checking prefab sensor name: '{prefabSensorName}'");
                
                if (prefabSensorName != sensorName) continue;
                
                Debug.Log($"[SensorManager] ✅ Found matching sensor prefab, creating instance...");
                var created = Instantiate(prefab, parent);
                created.prefab = prefab.gameObject;
                
                string parentInfo = parent != null ? parent.name : "Global (null parent)";
                Debug.Log($"[SensorManager] ✅ Successfully created sensor '{sensorName}' on '{parentInfo}'");
                return created;
            }
            
            Debug.LogWarning($"[SensorManager] ❌ No sensor found with name '{sensorName}'");
            return null;
        }
        
                // 获取测试用的父物体
        private Transform GetTestParent()
        {
            if (testParent != null) return testParent;
            return this.transform; // 如果没有指定，就用自己作为父物体
        }
        
        #region #Test
        
        [ContextMenu("List All Registered Sensors")]
        public void TestListRegisteredSensors()
        {
            var sensorNames = GetRegisteredSensorNames();
            Debug.Log($"[SensorManager] 📋 Registered Sensors ({sensorNames.Count}):");
            for (int i = 0; i < sensorNames.Count; i++)
            {
                Debug.Log($"  {i + 1}. {sensorNames[i]}");
            }
        }
        
        [ContextMenu("Create VirtualLightSensor")]
        public void TestCreateLightSensor()
        {
            var parent = GetTestParent();
            var sensor = CreateSensorByName("OPTICAL", parent);
            if (sensor != null)
            {
                Debug.Log($"[SensorManager] 💡 Light sensor created successfully on {parent.name}");
            }
        }
        
        [ContextMenu("Create VirtualDistanceSensor")]
        public void TestCreateDistanceSensor()
        {
            var parent = GetTestParent();
            var sensor = CreateSensorByName("DISTANCE", parent);
            if (sensor != null)
            {
                Debug.Log($"[SensorManager] 📏 Distance sensor created successfully on {parent.name}");
            }
        }
        
        [ContextMenu("Create All Available Sensors")]
        public void TestCreateAllSensors()
        {
            var parent = GetTestParent();
            var sensorNames = GetRegisteredSensorNames();
            
            Debug.Log($"[SensorManager] 🏭 Creating all {sensorNames.Count} available sensors...");
            
            foreach (var sensorName in sensorNames)
            {
                var sensor = CreateSensorByName(sensorName, parent);
                if (sensor != null)
                {
                    // 给每个传感器一个稍微不同的位置，避免重叠
                    sensor.transform.localPosition += new Vector3(
                        Random.Range(-0.5f, 0.5f), 
                        Random.Range(-0.5f, 0.5f), 
                        Random.Range(-0.5f, 0.5f)
                    );
                }
            }
        }
        
        [ContextMenu("Clear All Child Sensors")]
        public void TestClearAllSensors()
        {
            var parent = GetTestParent();
            var childSensors = parent.GetComponentsInChildren<VirtualSensor>();
            
            Debug.Log($"[SensorManager] 🧹 Clearing {childSensors.Length} sensors from {parent.name}...");
            
            for (int i = childSensors.Length - 1; i >= 0; i--)
            {
                if (childSensors[i] != null)
                {
                    string sensorName = childSensors[i].name;
                    if (Application.isPlaying)
                    {
                        Destroy(childSensors[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(childSensors[i].gameObject);
                    }
                    Debug.Log($"[SensorManager] 🗑️ Removed {sensorName}");
                }
            }
        }
        
        [ContextMenu("Test Sensor Functionality")]
        public void TestSensorFunctionality()
        {
            var parent = GetTestParent();
            var sensors = parent.GetComponentsInChildren<VirtualSensor>();
            
            Debug.Log($"[SensorManager] 🔍 Testing {sensors.Length} sensors...");
            
            foreach (var sensor in sensors)
            {
                if (sensor != null)
                {
                    var definition = sensor.SensorDefinition();
                    Debug.Log($"[SensorManager] 📊 {sensor.name}: {definition.getSensorName()}");
                    
                    // 如果传感器有描述方法，调用它
                    try
                    {
                        var description = sensor.GetSensorDescription();
                        Debug.Log($"[SensorManager] 📝 Description: {description}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[SensorManager] ⚠️ Failed to get description for {sensor.name}: {ex.Message}");
                    }
                }
            }
        }
        
        #endregion
    }
}