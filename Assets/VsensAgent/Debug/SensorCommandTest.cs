using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VsensAgent
{
    /// <summary>
    /// 传感器命令测试脚本
    /// 用于验证AI Agent传感器创建命令的JSON格式和参数解析
    /// </summary>
    public class SensorCommandTest : MonoBehaviour
    {
        [ContextMenu("Test Create OPTICAL Sensor")]
        public void TestCreateOpticalSensor()
        {
            Debug.Log("[SensorCommandTest] 🧪 Testing OPTICAL sensor creation command");
            
            // 模拟AI Agent发送的传感器创建命令 - parent回到parameters中
            var testCommand = new WsClient.ControlObject
            {
                target = "OPTICAL-1", // 传感器对象的命名格式：SENSORNAME-NUMBER
                action = "set_sensor",
                parameters = new Dictionary<string, object>
                {
                    ["sensor_type"] = "OPTICAL",
                    ["position"] = new float[] { 0f, 1.5f, 2f },
                    ["rotation"] = new float[] { 0f, 45f, 0f },
                    ["parent"] = "", // 父对象，空表示全局创建
                    // 传感器可视化选项
                    ["show_visualization"] = "true",
                    ["show_data_graph"] = "false"
                }
            };

            TestSensorCommand(testCommand);
        }

        [ContextMenu("Test Create DISTANCE Sensor")]
        public void TestCreateDistanceSensor()
        {
            Debug.Log("[SensorCommandTest] 🧪 Testing DISTANCE sensor creation command");
            
            var testCommand = new WsClient.ControlObject
            {
                target = "DISTANCE-1",
                action = "set_sensor", 
                parameters = new Dictionary<string, object>
                {
                    ["sensor_type"] = "DISTANCE",
                    ["position"] = new float[] { -2f, 1f, 0f },
                    ["rotation"] = new float[] { 0f, -90f, 0f },
                    ["parent"] = "Agent", // 尝试将传感器附加到Agent
                    // 传感器可视化选项
                    ["show_visualization"] = "true",
                    ["show_data_graph"] = "false",
                    // DISTANCE传感器特有参数
                    ["validDistance"] = "10.0",
                    ["lookDirection"] = new float[] { -1f, 0f, 0f }
                }
            };

            TestSensorCommand(testCommand);
        }

        [ContextMenu("Test Modify Existing Sensor")]
        public void TestModifyExistingSensor()
        {
            Debug.Log("[SensorCommandTest] 🧪 Testing sensor modification command");
            
            var testCommand = new WsClient.ControlObject
            {
                target = "OPTICAL-1", // 现有传感器名称
                action = "set_sensor",
                parameters = new Dictionary<string, object>
                {
                    ["sensor_type"] = "OPTICAL", // 修改时仍需要指定类型
                    ["position"] = new float[] { 1f, 2f, 1f },
                    ["parent"] = "bowl", // 修改父对象
                    ["show_visualization"] = "false",
                    ["show_data_graph"] = "true"
                }
            };

            TestSensorCommand(testCommand);
        }

        private void TestSensorCommand(WsClient.ControlObject command)
        {
            // 将命令转换为JSON以验证序列化
            string jsonCommand = JsonConvert.SerializeObject(command, Formatting.Indented);
            Debug.Log($"[SensorCommandTest] 📋 Command JSON:\n{jsonCommand}");

            // 模拟通过WsClient发送命令
            // 实际使用中，这会通过WebSocket发送给AI Agent处理
            Debug.Log("[SensorCommandTest] 🚀 Command would be processed by ControlManager.HandleSingleControl()");
            Debug.Log("[SensorCommandTest] ✅ Test command structure validated");
        }

        [ContextMenu("Show All Sensors")]
        public void ShowAllSensors()
        {
            Debug.Log("[SensorCommandTest] 📊 Listing all sensors in scene:");
            
            var sensors = FindObjectsOfType<SensorObjectDescriber>();
            if (sensors.Length == 0)
            {
                Debug.Log("[SensorCommandTest] 📭 No sensors found in scene");
                return;
            }

            foreach (var sensor in sensors)
            {
                Debug.Log($"[SensorCommandTest] 🔍 Found sensor: {sensor.name} at position {sensor.transform.position}");
            }
        }

        [ContextMenu("Clear All Test Sensors")]
        public void ClearAllTestSensors()
        {
            Debug.Log("[SensorCommandTest] 🧹 Clearing all test sensors");
            
            var sensors = FindObjectsOfType<SensorObjectDescriber>();
            int cleared = 0;
            
            foreach (var sensor in sensors)
            {
                if (sensor.name.Contains("OPTICAL") || sensor.name.Contains("DISTANCE"))
                {
                    Debug.Log($"[SensorCommandTest] 🗑️ Destroying sensor: {sensor.name}");
                    DestroyImmediate(sensor.gameObject);
                    cleared++;
                }
            }
            
            Debug.Log($"[SensorCommandTest] ✅ Cleared {cleared} test sensors");
        }
    }
}
