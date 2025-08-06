using UnityEngine;
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VsensAgent.VirtualObject.Sensor;
using Sensor;

namespace VsensAgent
{
    public class ControlManager : MonoBehaviour
    {
        void OnEnable()
        {
            WsClient.OnControl += HandleControlBatch; // 统一处理批量控制
            Debug.Log("[ControlManager] 🔌 ControlManager enabled and listening for control events.");
        }

        void OnDisable()
        {
            WsClient.OnControl -= HandleControlBatch;
            Debug.Log("[ControlManager] 🔌 ControlManager disabled.");
        }

        private void HandleControlBatch(WsClient.ControlObject[] controlActions)
        {
            if (controlActions == null || controlActions.Length == 0)
            {
                Debug.LogWarning("[ControlManager] ⚠️ Received null or empty control actions array.");
                return;
            }

            Debug.Log($"[ControlManager] 📦 Processing {controlActions.Length} control action(s)");
            
            for (int i = 0; i < controlActions.Length; i++)
            {
                var ctrl = controlActions[i];
                Debug.Log($"[ControlManager] 🎯 Processing action {i + 1}/{controlActions.Length}: target='{ctrl?.target}', action='{ctrl?.action}'");
                HandleSingleControl(ctrl);
            }
        }

        private void HandleSingleControl(WsClient.ControlObject ctrl)
        {
            if (ctrl == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ Received null control command.");
                return;
            }

            Debug.Log($"[ControlManager] 🎮 Handling control: {ctrl.action} on {ctrl.target}");

            // set_sensor 命令的特殊处理（可能不需要现有的target object）
            if (ctrl.action == "set_sensor")
            {
                HandleSetSensorAction(ctrl);
                return;
            }

            // 其他命令需要target object
            if (string.IsNullOrEmpty(ctrl.target))
            {
                Debug.LogWarning("[ControlManager] ⚠️ Received invalid control command - missing target.");
                return;
            }

            // 1. 查找场景物体
            GameObject targetObj = GameObject.Find(ctrl.target);
            if (targetObj == null)
            {
                Debug.LogWarning($"[ControlManager] ❌ Target object '{ctrl.target}' not found.");
                return;
            }

            // 2. 通过 ObjectDescriber 检查物品属性，更智能地处理
            ObjectDescriber describer = targetObj.GetComponent<ObjectDescriber>();
            if (describer != null)
            {
                HandleControlWithProperties(targetObj, describer, ctrl);
            }
            else
            {
                // 3. 如果没有ObjectDescriber，回退到原来的方式
                HandleControlWithoutProperties(targetObj, ctrl);
            }
        }

        private void HandleControlWithProperties(GameObject targetObj, ObjectDescriber describer, WsClient.ControlObject ctrl)
        {
            Debug.Log($"[ControlManager] 📋 Object '{ctrl.target}' has properties: [{string.Join(", ", describer.GetProperties().ToArray())}]");

            // 根据action和属性来决定处理方式
            switch (ctrl.action)
            {
                case "set_state":
                    if (describer.HasProperty("with_state"))
                    {
                        StateObject stateObj = targetObj.GetComponent<StateObject>();
                        if (stateObj != null)
                        {
                            HandleStateObjectControl(stateObj, ctrl);
                        }
                        else
                        {
                            Debug.LogWarning($"[ControlManager] ⚠️ Object '{ctrl.target}' has 'with_state' property but no StateObject component!");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[ControlManager] ⚠️ Object '{ctrl.target}' doesn't have 'with_state' property, cannot set state.");
                    }
                    break;

                case "set_transform":
                    if (describer.HasProperty("movable"))
                    {
                        HandleTransformAction(targetObj, ctrl);
                    }
                    else
                    {
                        Debug.LogWarning($"[ControlManager] ⚠️ Object '{ctrl.target}' is not movable, cannot perform transform action.");
                    }
                    break;

                case "highlight":
                    // 高亮不需要特定属性，任何物品都可以高亮
                    HandleHighlightAction(targetObj, ctrl);
                    break;

                case "set_sensor":
                    // 传感器命令需要特殊处理，因为可能创建新对象
                    HandleSetSensorAction(ctrl);
                    return; // 直接返回，不需要targetObj

                default:
                    Debug.LogWarning($"[ControlManager] ⚠️ Unsupported action '{ctrl.action}' for object '{ctrl.target}'.");
                    break;
            }
        }

        private void HandleControlWithoutProperties(GameObject targetObj, WsClient.ControlObject ctrl)
        {
            Debug.Log($"[ControlManager] 🔄 Object '{ctrl.target}' has no ObjectDescriber, using legacy detection.");

            // 原来的方式：直接查找组件
            StateObject stateObj = targetObj.GetComponent<StateObject>();
            if (stateObj != null)
            {
                HandleStateObjectControl(stateObj, ctrl);
                return;
            }

            // 其他组件检查...
            Debug.LogWarning($"[ControlManager] ⚠️ Target '{ctrl.target}' has no supported components for action '{ctrl.action}'.");
        }

        private void HandleStateObjectControl(StateObject obj, WsClient.ControlObject ctrl)
        {
            switch (ctrl.action)
            {
                case "set_state":
                    if (ctrl.parameters != null && ctrl.parameters.ContainsKey("state"))
                    {
                        string newState = ParseStringFromParameter(ctrl.parameters["state"]);
                        obj.setCurrentState(newState);
                        Debug.Log($"[ControlManager] ✅ Set '{obj.name}' to state '{newState}'");
                    }
                    else
                    {
                        Debug.LogWarning("[ControlManager] ⚠️ 'set_state' requires 'state' parameter.");
                    }
                    break;

                default:
                    Debug.LogWarning($"[ControlManager] ⚠️ Unsupported action '{ctrl.action}' for StateObject.");
                    break;
            }
        }

        private void HandleTransformAction(GameObject obj, WsClient.ControlObject ctrl)
        {
            Debug.Log($"[ControlManager] 🔄 Performing transform action on '{obj.name}'");
            
            if (ctrl.parameters == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ Transform action requires parameters.");
                return;
            }

            bool transformChanged = false;

            // 处理位置参数 - 后端格式：position: [x, y, z]
            if (ctrl.parameters.ContainsKey("position"))
            {
                Vector3 newPosition = ParseVector3FromArray(ctrl.parameters["position"], obj.transform.position);
                if (newPosition != obj.transform.position)
                {
                    obj.transform.position = newPosition;
                    Debug.Log($"[ControlManager] 📍 Set position of '{obj.name}' to {newPosition}");
                    transformChanged = true;
                }
            }

            // 处理旋转参数 - 后端格式：rotation: [x, y, z]
            if (ctrl.parameters.ContainsKey("rotation"))
            {
                Vector3 newRotation = ParseVector3FromArray(ctrl.parameters["rotation"], obj.transform.eulerAngles);
                if (newRotation != obj.transform.eulerAngles)
                {
                    obj.transform.rotation = Quaternion.Euler(newRotation);
                    Debug.Log($"[ControlManager] 🔄 Set rotation of '{obj.name}' to {newRotation}");
                    transformChanged = true;
                }
            }

            // 处理缩放参数 - 后端格式：scale: [x, y, z]
            if (ctrl.parameters.ContainsKey("scale"))
            {
                Vector3 newScale = ParseVector3FromArray(ctrl.parameters["scale"], obj.transform.localScale);
                if (newScale != obj.transform.localScale)
                {
                    obj.transform.localScale = newScale;
                    Debug.Log($"[ControlManager] 📏 Set scale of '{obj.name}' to {newScale}");
                    transformChanged = true;
                }
            }

            if (transformChanged)
            {
                Debug.Log($"[ControlManager] ✅ Successfully transformed '{obj.name}'");
            }
            else
            {
                Debug.LogWarning($"[ControlManager] ⚠️ No valid transform parameters found for '{obj.name}'");
            }
        }

        /// <summary>
        /// 专门用于set_transform：从JSON数组解析Vector3
        /// 后端格式：position/rotation/scale: [x, y, z]
        /// </summary>
        private Vector3 ParseVector3FromArray(object paramValue, Vector3 defaultValue)
        {
            try
            {
                if (paramValue is Newtonsoft.Json.Linq.JArray jsonArray)
                {
                    if (jsonArray.Count >= 3)
                    {
                        float x = jsonArray[0].Value<float>();
                        float y = jsonArray[1].Value<float>();
                        float z = jsonArray[2].Value<float>();
                        return new Vector3(x, y, z);
                    }
                    else
                    {
                        Debug.LogWarning($"[ControlManager] ⚠️ Vector3 array must have 3 elements, got {jsonArray.Count}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ControlManager] ⚠️ Expected JSON array for Vector3, got {paramValue?.GetType()}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Failed to parse Vector3 array: {ex.Message}");
            }

            Debug.LogWarning($"[ControlManager] ⚠️ Using default value {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// 专门用于set_state：从参数中解析字符串
        /// 后端格式：state: "string_value"
        /// </summary>
        private string ParseStringFromParameter(object paramValue)
        {
            if (paramValue != null)
            {
                return paramValue.ToString();
            }
            
            Debug.LogWarning("[ControlManager] ⚠️ State parameter is null, using empty string");
            return "";
        }

        private void HandleHighlightAction(GameObject obj, WsClient.ControlObject ctrl)
        {
            Debug.Log($"[ControlManager] ✨ Highlighting '{obj.name}'");
            
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                // 保存原始颜色
                Color originalColor = renderer.material.color;
                
                // 设置高亮颜色
                Color highlightColor = Color.yellow;
                
                // 如果参数中指定了颜色
                if (ctrl.parameters != null && ctrl.parameters.ContainsKey("color"))
                {
                    string colorString = ParseStringFromParameter(ctrl.parameters["color"]);
                    if (ColorUtility.TryParseHtmlString(colorString, out Color customColor))
                    {
                        highlightColor = customColor;
                    }
                }
                
                // 应用高亮颜色
                renderer.material.color = highlightColor;
                Debug.Log($"[ControlManager] ✅ Highlighted '{obj.name}' with color {highlightColor}, will restore to {originalColor} in 5 seconds");
                
                // 启动协程在5秒后恢复原始颜色
                StartCoroutine(RestoreColorAfterDelay(renderer, originalColor, 5.0f, obj.name));
            }
            else
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Cannot highlight '{obj.name}' - no Renderer component found.");
            }
        }

        /// <summary>
        /// 在指定延迟后恢复物体的原始颜色
        /// </summary>
        /// <param name="renderer">渲染器组件</param>
        /// <param name="originalColor">原始颜色</param>
        /// <param name="delay">延迟时间（秒）</param>
        /// <param name="objectName">物体名称（用于日志）</param>
        private System.Collections.IEnumerator RestoreColorAfterDelay(Renderer renderer, Color originalColor, float delay, string objectName)
        {
            yield return new WaitForSeconds(delay);
            
            // 检查渲染器是否仍然存在（物体可能已被销毁）
            if (renderer != null && renderer.material != null)
            {
                renderer.material.color = originalColor;
                Debug.Log($"[ControlManager] 🔄 Restored original color {originalColor} for '{objectName}' after {delay} seconds");
            }
            else
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Cannot restore color for '{objectName}' - renderer or material no longer exists");
            }
        }

        // ===================== SENSOR CONTROL METHODS =====================

        /// <summary>
        /// 处理传感器创建和修改命令
        /// </summary>
        /// <param name="ctrl">传感器控制命令</param>
        private void HandleSetSensorAction(WsClient.ControlObject ctrl)
        {
            Debug.Log($"[ControlManager] 🔧 Processing sensor command: {ctrl.action}");

            try
            {
                // 1. 获取传感器类型（必需参数）
                if (ctrl.parameters == null || !ctrl.parameters.ContainsKey("sensor_type"))
                {
                    Debug.LogError("[ControlManager] ❌ sensor_type parameter is required for set_sensor command");
                    return;
                }

                string sensorType = ParseStringFromParameter(ctrl.parameters["sensor_type"]);
                if (string.IsNullOrEmpty(sensorType))
                {
                    Debug.LogError("[ControlManager] ❌ sensor_type parameter cannot be empty");
                    return;
                }

                // 2. 检查是否是修改现有传感器
                GameObject existingSensor = null;
                if (!string.IsNullOrEmpty(ctrl.target))
                {
                    existingSensor = GameObject.Find(ctrl.target);
                    if (existingSensor != null)
                    {
                        Debug.Log($"[ControlManager] 🔄 Modifying existing sensor: {ctrl.target}");
                    }
                    else
                    {
                        Debug.LogWarning($"[ControlManager] ⚠️ Target sensor '{ctrl.target}' not found, creating new sensor instead");
                    }
                }

                // 3. 创建或修改传感器
                GameObject sensorObj;
                if (existingSensor != null)
                {
                    sensorObj = existingSensor;
                    Debug.Log($"[ControlManager] 📝 Modifying sensor: {sensorObj.name}");
                }
                else
                {
                    // 创建新传感器 - 重要：始终以null parent创建，确保在global空间中
                    // 这样Agent给出的global坐标才能正确应用
                    Debug.Log($"[ControlManager] 🌍 Creating sensor in global space first (Agent coordinates are global)");
                    
                    // 检查VsensAgentSensorManager实例是否存在
                    if (VsensAgentSensorManager.Instance == null)
                    {
                        Debug.LogError("[ControlManager] ❌ VsensAgentSensorManager.Instance is null! Make sure VsensAgentSensorManager is in the scene.");
                        
                        // 尝试查找场景中的VsensAgentSensorManager
                        var sensorManagerInScene = FindFirstObjectByType<VsensAgentSensorManager>();
                        if (sensorManagerInScene != null)
                        {
                            Debug.LogWarning("[ControlManager] 🔧 Found VsensAgentSensorManager in scene but Instance is null. This suggests initialization issue.");
                        }
                        else
                        {
                            Debug.LogError("[ControlManager] ❌ No VsensAgentSensorManager found in scene!");
                        }
                        return;
                    }
                    
                    Debug.Log($"[ControlManager] 🔧 Creating sensor of type '{sensorType}' in global space (parent will be set later)");
                    
                    // 始终以null parent创建，确保在global空间
                    Debug.Log("[ControlManager] 🚀 Calling VsensAgentSensorManager.Instance.CreateSensorByName with null parent...");
                    VirtualSensor virtualSensor = null;
                    
                    try
                    {
                        virtualSensor = VsensAgentSensorManager.Instance.CreateSensorByName(sensorType, null);
                    }
                    catch (System.Exception createEx)
                    {
                        Debug.LogError($"[ControlManager] ❌ Exception during CreateSensorByName: {createEx.Message}");
                        Debug.LogError($"[ControlManager] 🔍 CreateSensorByName stack trace: {createEx.StackTrace}");
                        return;
                    }
                    
                    if (virtualSensor == null)
                    {
                        Debug.LogError($"[ControlManager] ❌ Failed to create sensor of type: {sensorType}");
                        return;
                    }
                    
                    Debug.Log($"[ControlManager] ✅ CreateSensorByName returned: {virtualSensor} (type: {virtualSensor.GetType().Name})");
                    
                    sensorObj = virtualSensor.gameObject;
                    if (sensorObj == null)
                    {
                        Debug.LogError("[ControlManager] ❌ Created VirtualSensor has null gameObject!");
                        return;
                    }
                    
                    Debug.Log($"[ControlManager] ✅ Created new sensor: {sensorObj.name} (GameObject valid: {sensorObj != null})");
                }

                // 4. 应用Agent的global变换参数 (必须在设置parent之前)
                Debug.Log("[ControlManager] 🌍 Applying Agent's global transform parameters...");
                if (sensorObj != null)
                {
                    Debug.Log($"[ControlManager] 📍 sensorObj is valid: {sensorObj.name} (active: {sensorObj.activeInHierarchy})");
                    ApplyTransformParameters(sensorObj, ctrl.parameters);
                }
                else
                {
                    Debug.LogError("[ControlManager] ❌ Cannot apply transform parameters - sensorObj is null");
                    return;
                }

                // 5. 应用传感器特定参数 (包括parent设置，必须在应用global坐标之后)
                Debug.Log("[ControlManager] 🔧 Applying sensor-specific parameters (including parent setup)...");
                if (sensorObj != null && ctrl.parameters != null)
                {
                    ApplySensorSpecificParameters(sensorObj, ctrl.parameters);
                }
                else
                {
                    Debug.LogError("[ControlManager] ❌ Cannot apply sensor parameters - sensorObj or parameters is null");
                    return;
                }

                Debug.Log($"[ControlManager] 🎯 Sensor command completed successfully for: {sensorObj.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ControlManager] ❌ Error in HandleSetSensorAction: {e.Message}");
                Debug.LogError($"[ControlManager] 🔍 Stack trace: {e.StackTrace}");
                
                // 提供更详细的错误信息
                if (e is System.NullReferenceException)
                {
                    Debug.LogError("[ControlManager] 💡 This is a NullReferenceException. Check if:");
                    Debug.LogError("  - VsensAgentSensorManager.Instance is not null");
                    Debug.LogError("  - CreateSensorByName returned a valid VirtualSensor");
                    Debug.LogError("  - VirtualSensor.gameObject is not null");
                    Debug.LogError("  - All required components are attached to the sensor prefab");
                }
            }
        }

        /// <summary>
        /// 应用传感器特定的参数
        /// </summary>
        /// <param name="sensorObj">传感器游戏对象</param>
        /// <param name="parameters">参数字典</param>
        private void ApplySensorSpecificParameters(GameObject sensorObj, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            if (sensorObj == null)
            {
                Debug.LogError("[ControlManager] ❌ ApplySensorSpecificParameters: sensorObj is null");
                return;
            }
            
            if (parameters == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ ApplySensorSpecificParameters: parameters is null");
                return;
            }

            Debug.Log($"[ControlManager] 🔧 Applying sensor-specific parameters to {sensorObj.name}");

            // 获取SensorObjectDescriber组件
            SensorObjectDescriber sensorDescriber = sensorObj.GetComponent<SensorObjectDescriber>();
            if (sensorDescriber == null)
            {
                Debug.LogWarning($"[ControlManager] ⚠️ No SensorObjectDescriber found on {sensorObj.name}");
                return;
            }

            // 处理show_visualization参数
            if (parameters.ContainsKey("show_visualization"))
            {
                string showVisString = ParseStringFromParameter(parameters["show_visualization"]);
                bool showVis = bool.Parse(showVisString);
                Debug.Log($"[ControlManager] 👁️ Setting show_visualization to {showVis} for {sensorObj.name}");
                // 这里可以添加具体的可视化控制逻辑
            }

            // 处理show_data_graph参数
            if (parameters.ContainsKey("show_data_graph"))
            {
                string showDataString = ParseStringFromParameter(parameters["show_data_graph"]);
                bool showData = bool.Parse(showDataString);
                Debug.Log($"[ControlManager] 📊 Setting show_data_graph to {showData} for {sensorObj.name}");
                // 这里可以添加具体的数据图表控制逻辑
            }

            // 处理validDistance参数 (仅用于DISTANCE传感器)
            if (parameters.ContainsKey("validDistance"))
            {
                string distanceString = ParseStringFromParameter(parameters["validDistance"]);
                float distance = float.Parse(distanceString);
                Debug.Log($"[ControlManager] 📏 Setting validDistance to {distance} for {sensorObj.name}");
                // 这里可以添加具体的距离设置逻辑
            }

            // 处理lookDirection参数 (仅用于DISTANCE传感器)
            if (parameters.ContainsKey("lookDirection"))
            {
                Vector3 lookDir = ParseVector3FromArray(parameters["lookDirection"], Vector3.forward);
                Debug.Log($"[ControlManager] 👀 Setting lookDirection to {lookDir} for {sensorObj.name}");
                // 应用朝向
                if (lookDir != Vector3.zero)
                {
                    sensorObj.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }

            // 处理parent参数 (重要：必须在应用global坐标之后设置)
            // Agent给出的坐标是global的，我们先应用了这些坐标，现在设置parent让Unity自动转换为local坐标
            if (parameters.ContainsKey("parent"))
            {
                string parentName = ParseStringFromParameter(parameters["parent"]);
                if (!string.IsNullOrEmpty(parentName))
                {
                    GameObject parentObj = GameObject.Find(parentName);
                    if (parentObj != null)
                    {
                        Debug.Log($"[ControlManager] 🌍➡️👨‍👧‍👦 Converting from global to local space by setting parent '{parentName}' for {sensorObj.name}");
                        Debug.Log($"[ControlManager] 📍 Before parent: position={sensorObj.transform.position}, rotation={sensorObj.transform.eulerAngles}");
                        
                        sensorObj.transform.SetParent(parentObj.transform);
                        
                        Debug.Log($"[ControlManager] 📍 After parent: position={sensorObj.transform.position}, rotation={sensorObj.transform.eulerAngles}");
                        Debug.Log($"[ControlManager] 📍 Local coordinates: position={sensorObj.transform.localPosition}, rotation={sensorObj.transform.localEulerAngles}");
                    }
                    else
                    {
                        Debug.LogWarning($"[ControlManager] ⚠️ Parent object '{parentName}' not found");
                    }
                }
                else
                {
                    // 空字符串表示移除父对象 (回到global space)
                    Debug.Log($"[ControlManager] 🆓 Removing parent from {sensorObj.name} (back to global space)");
                    sensorObj.transform.SetParent(null);
                }
            }
        }

        /// <summary>
        /// 应用Agent给出的global变换参数到游戏对象
        /// 注意：Agent给出的所有坐标和旋转都是global coordinate，必须在设置parent之前应用
        /// </summary>
        /// <param name="obj">目标游戏对象</param>
        /// <param name="parameters">参数字典</param>
        private void ApplyTransformParameters(GameObject obj, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            if (obj == null)
            {
                Debug.LogError("[ControlManager] ❌ ApplyTransformParameters: obj is null");
                return;
            }
            
            if (parameters == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ ApplyTransformParameters: parameters is null");
                return;
            }

            Debug.Log($"[ControlManager] 🌍 Applying Agent's global transform parameters to {obj.name}");

            // 处理位置参数 - Agent给出的是global坐标
            if (parameters.ContainsKey("position"))
            {
                Vector3 newPosition = ParseVector3FromArray(parameters["position"], obj.transform.position);
                obj.transform.position = newPosition;
                Debug.Log($"[ControlManager] 📍 Set global position of '{obj.name}' to {newPosition}");
            }

            // 处理旋转参数 - Agent给出的是global旋转
            if (parameters.ContainsKey("rotation"))
            {
                Vector3 newRotation = ParseVector3FromArray(parameters["rotation"], obj.transform.eulerAngles);
                obj.transform.rotation = Quaternion.Euler(newRotation);
                Debug.Log($"[ControlManager] 🔄 Set global rotation of '{obj.name}' to {newRotation}");
            }

            // 注意：根据用户反馈，删除了scale参数支持，因为用户不太会去调整传感器的大小
        }

        // ===================== END SENSOR CONTROL METHODS =====================
    }
}
