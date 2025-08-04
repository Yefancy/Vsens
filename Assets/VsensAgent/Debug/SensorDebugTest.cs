using UnityEngine;
using System.Collections.Generic;

namespace VsensAgent
{
    /// <summary>
    /// 调试传感器创建问题的测试脚本
    /// </summary>
    public class SensorDebugTest : MonoBehaviour
    {
        [ContextMenu("Test Empty Target Sensor Creation")]
        public void TestEmptyTargetSensorCreation()
        {
            Debug.Log("[SensorDebugTest] 🧪 Testing sensor creation with empty target (simulating AI Agent command)");
            
            // 模拟AI Agent发送的命令，target为空字符串
            var testCommand = new WsClient.ControlObject
            {
                target = "", // 空target，应该创建新传感器
                action = "set_sensor",
                parameters = new Dictionary<string, object>
                {
                    ["sensor_type"] = "OPTICAL",
                    ["parent"] = "", // 空parent，全局创建 - 这可能是问题所在！
                    ["position"] = new float[] { 1.722f, 3.396f, 7.546f },
                    ["rotation"] = new float[] { 0f, 0f, 0f },
                    ["show_visualization"] = "true",
                    ["show_data_graph"] = "false"
                }
            };

            Debug.Log("[SensorDebugTest] 📋 Command details:");
            Debug.Log($"  target: '{testCommand.target}' (empty = create new)");
            Debug.Log($"  action: '{testCommand.action}'");
            Debug.Log($"  sensor_type: '{testCommand.parameters["sensor_type"]}'");
            Debug.Log($"  parent: '{testCommand.parameters["parent"]}' (empty = global)");
            Debug.Log($"  position: [{string.Join(", ", (float[])testCommand.parameters["position"])}]");

            // 检查场景中的关键组件
            CheckSceneComponents();

            // 获取ControlManager并测试
            ControlManager controlManager = FindFirstObjectByType<ControlManager>();
            if (controlManager != null)
            {
                Debug.Log("[SensorDebugTest] ✅ ControlManager found");
                
                // 模拟调用HandleControlBatch
                var commands = new WsClient.ControlObject[] { testCommand };
                try
                {
                    // 通过反射调用私有方法进行测试
                    var method = typeof(ControlManager).GetMethod("HandleControlBatch", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method.Invoke(controlManager, new object[] { commands });
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SensorDebugTest] ❌ Exception during test: {e.Message}");
                    Debug.LogError($"[SensorDebugTest] ❌ Stack trace: {e.StackTrace}");
                    
                    if (e.InnerException != null)
                    {
                        Debug.LogError($"[SensorDebugTest] 🔍 Inner exception: {e.InnerException.Message}");
                        Debug.LogError($"[SensorDebugTest] 🔍 Inner stack trace: {e.InnerException.StackTrace}");
                    }
                }
            }
            else
            {
                Debug.LogError("[SensorDebugTest] ❌ ControlManager not found in scene!");
            }
        }

        [ContextMenu("Check Scene Components")]
        public void CheckSceneComponents()
        {
            Debug.Log("[SensorDebugTest] 🔍 Checking scene components...");
            
            // 检查VsensAgentSensorManager
            var sensorManager = FindFirstObjectByType<VsensAgent.VirtualObject.Sensor.VsensAgentSensorManager>();
            if (sensorManager != null)
            {
                Debug.Log($"[SensorDebugTest] ✅ VsensAgentSensorManager found: {sensorManager.name}");
                Debug.Log($"[SensorDebugTest] 📊 Instance check: {VsensAgent.VirtualObject.Sensor.VsensAgentSensorManager.Instance != null}");
            }
            else
            {
                Debug.LogError("[SensorDebugTest] ❌ VsensAgentSensorManager not found in scene!");
            }

            // 检查ControlManager
            var controlManager = FindFirstObjectByType<ControlManager>();
            if (controlManager != null)
            {
                Debug.Log($"[SensorDebugTest] ✅ ControlManager found: {controlManager.name}");
            }
            else
            {
                Debug.LogError("[SensorDebugTest] ❌ ControlManager not found in scene!");
            }

            // 检查WsClient
            var wsClient = FindFirstObjectByType<WsClient>();
            if (wsClient != null)
            {
                Debug.Log($"[SensorDebugTest] ✅ WsClient found: {wsClient.name}");
            }
            else
            {
                Debug.LogWarning("[SensorDebugTest] ⚠️ WsClient not found in scene");
            }
        }

        [ContextMenu("Test Step By Step Sensor Creation")]
        public void TestStepByStepSensorCreation()
        {
            Debug.Log("[SensorDebugTest] 🧪 Testing step-by-step sensor creation...");
            
            // Step 1: Check VsensAgentSensorManager
            Debug.Log("[SensorDebugTest] 📋 Step 1: Checking VsensAgentSensorManager...");
            var sensorManager = VsensAgent.VirtualObject.Sensor.VsensAgentSensorManager.Instance;
            if (sensorManager == null)
            {
                Debug.LogError("[SensorDebugTest] ❌ VsensAgentSensorManager.Instance is null!");
                return;
            }
            Debug.Log($"[SensorDebugTest] ✅ VsensAgentSensorManager.Instance found: {sensorManager.name}");

            // Step 2: Check available sensors
            Debug.Log("[SensorDebugTest] � Step 2: Checking available sensors...");
            var availableSensors = sensorManager.GetRegisteredSensorNames();
            Debug.Log($"[SensorDebugTest] 📊 Available sensors: {string.Join(", ", availableSensors.ToArray())}");
            
            if (!availableSensors.Contains("OPTICAL"))
            {
                Debug.LogError("[SensorDebugTest] ❌ OPTICAL sensor not found in registered sensors!");
                return;
            }

            // Step 3: Test sensor creation with null parent (global)
            Debug.Log("[SensorDebugTest] 📋 Step 3: Creating sensor with null parent...");
            try
            {
                var virtualSensor = sensorManager.CreateSensorByName("OPTICAL", null);
                if (virtualSensor == null)
                {
                    Debug.LogError("[SensorDebugTest] ❌ CreateSensorByName returned null!");
                    return;
                }
                
                Debug.Log($"[SensorDebugTest] ✅ VirtualSensor created: {virtualSensor.name}");
                
                // Step 4: Check GameObject
                if (virtualSensor.gameObject == null)
                {
                    Debug.LogError("[SensorDebugTest] ❌ VirtualSensor.gameObject is null!");
                    return;
                }
                
                Debug.Log($"[SensorDebugTest] ✅ GameObject is valid: {virtualSensor.gameObject.name}");
                
                // Step 5: Check SensorObjectDescriber component
                var describer = virtualSensor.GetComponent<SensorObjectDescriber>();
                if (describer == null)
                {
                    Debug.LogWarning("[SensorDebugTest] ⚠️ No SensorObjectDescriber component found");
                }
                else
                {
                    Debug.Log($"[SensorDebugTest] ✅ SensorObjectDescriber found: {describer.GetType().Name}");
                }
                
                // Step 6: Test position setting
                Debug.Log("[SensorDebugTest] 📋 Step 6: Testing position setting...");
                Vector3 testPosition = new Vector3(1.722f, 3.396f, 7.546f);
                virtualSensor.transform.position = testPosition;
                Debug.Log($"[SensorDebugTest] ✅ Position set to: {virtualSensor.transform.position}");
                
                Debug.Log("[SensorDebugTest] 🎉 All steps completed successfully!");
                
                // Clean up
                DestroyImmediate(virtualSensor.gameObject);
                Debug.Log("[SensorDebugTest] 🧹 Test sensor cleaned up");
                
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SensorDebugTest] ❌ Exception during step-by-step creation: {e.Message}");
                Debug.LogError($"[SensorDebugTest] 🔍 Stack trace: {e.StackTrace}");
            }
        }
    }
}
