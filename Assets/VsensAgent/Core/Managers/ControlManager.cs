using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VsensAgent.VirtualObject.Sensor;
using Sensor;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;
using VsensAgent.Core;
using VsensAgent.RuntimeEditing;
using VsensAgent.SceneApi.V2;
using VsensAgent.SceneHistory;

namespace VsensAgent
{
    public class ControlManager : MonoBehaviour
    {
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private SceneRegistry sceneRegistry;
        [SerializeField] private AvatarRuntimeManager avatarRuntimeManager;
        [SerializeField] private SceneActionHistory sceneActionHistory;
        void OnEnable()
        {
            WsClient.OnControl += HandleControlBatch; // 统一处理批量控制
            ResolveSceneRegistry();
            Debug.Log("[ControlManager] 🔌 ControlManager enabled and listening for control events.");
        }

        void OnDisable()
        {
            WsClient.OnControl -= HandleControlBatch;
            Debug.Log("[ControlManager] 🔌 ControlManager disabled.");
        }

        public bool TryValidateControlAction(ControlObject ctrl, out string errorCode, out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;

            if (ctrl == null)
            {
                errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                errorMessage = "Control action is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ctrl.action))
            {
                errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                errorMessage = "Missing action field.";
                return false;
            }

            if (ctrl.action == "set_sensor")
            {
                if (ctrl.parameters == null || !ctrl.parameters.ContainsKey("sensor_type"))
                {
                    errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                    errorMessage = "set_sensor requires 'sensor_type'.";
                    return false;
                }
                if (IsAvatarJointAttach(ctrl.parameters))
                {
                    ResolveAvatarRuntimeManager();
                    if (avatarRuntimeManager == null)
                    {
                        errorCode = SceneApi.V2.SceneApiErrorCodes.CONSTRAINT_VIOLATION;
                        errorMessage = "AvatarRuntimeManager not found.";
                        return false;
                    }

                    var avatarId = ParseStringFromParameter(ctrl.parameters["avatar_id"]);
                    var jointName = ParseStringFromParameter(ctrl.parameters["joint_name"]);
                    if (string.IsNullOrWhiteSpace(avatarId) || string.IsNullOrWhiteSpace(jointName))
                    {
                        errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                        errorMessage = "set_sensor with attach_mode=avatar_joint requires 'avatar_id' and 'joint_name'.";
                        return false;
                    }
                }
                return true;
            }

            if (ctrl.action == "remove_sensor")
            {
                if (string.IsNullOrWhiteSpace(ctrl.target))
                {
                    errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                    errorMessage = "remove_sensor requires target sensor object name.";
                    return false;
                }

                ResolveSensorManager();
                if (sensorManager == null)
                {
                    errorCode = SceneApi.V2.SceneApiErrorCodes.CONSTRAINT_VIOLATION;
                    errorMessage = "VsensAgentSensorManager not found.";
                    return false;
                }

                var sensor = FindObjectsByType<VirtualSensor>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate != null && candidate.name == ctrl.target);
                if (sensor == null)
                {
                    errorCode = SceneApi.V2.SceneApiErrorCodes.TARGET_NOT_FOUND;
                    errorMessage = $"Sensor '{ctrl.target}' not found.";
                    return false;
                }

                return true;
            }

            if (ctrl.action == "delegate_task")
            {
                return true;
            }

            if (IsAvatarAction(ctrl.action))
            {
                return TryValidateAvatarAction(ctrl, out errorCode, out errorMessage);
            }

            if (string.IsNullOrWhiteSpace(ctrl.target))
            {
                errorCode = SceneApi.V2.SceneApiErrorCodes.TARGET_NOT_FOUND;
                errorMessage = "Missing target for non-sensor action.";
                return false;
            }

            var targetObj = GameObject.Find(ctrl.target);
            if (targetObj == null)
            {
                errorCode = SceneApi.V2.SceneApiErrorCodes.TARGET_NOT_FOUND;
                errorMessage = $"Target '{ctrl.target}' not found.";
                return false;
            }

            switch (ctrl.action)
            {
                case "set_state":
                    if (ctrl.parameters == null || !ctrl.parameters.ContainsKey("state"))
                    {
                        errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                        errorMessage = "set_state requires 'state'.";
                        return false;
                    }
                    if (!targetObj.TryGetComponent<StateObject>(out _))
                    {
                        errorCode = SceneApi.V2.SceneApiErrorCodes.UNSUPPORTED_ACTION;
                        errorMessage = $"Target '{ctrl.target}' does not support set_state.";
                        return false;
                    }
                    return true;

                case "set_transform":
                    if (ctrl.parameters == null ||
                        (!ctrl.parameters.ContainsKey("position") &&
                         !ctrl.parameters.ContainsKey("rotation") &&
                         !ctrl.parameters.ContainsKey("scale")))
                    {
                        errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                        errorMessage = "set_transform requires one of position/rotation/scale.";
                        return false;
                    }
                    return true;

                case "highlight":
                    return true;

                default:
                    errorCode = SceneApi.V2.SceneApiErrorCodes.UNSUPPORTED_ACTION;
                    errorMessage = $"Unsupported action '{ctrl.action}'.";
                    return false;
            }
        }

        public bool TryExecuteControlAction(ControlObject ctrl, out string errorCode, out string errorMessage)
        {
            if (!TryValidateControlAction(ctrl, out errorCode, out errorMessage))
            {
                return false;
            }

            if (IsAvatarAction(ctrl.action))
            {
                return TryExecuteAvatarAction(ctrl, out errorCode, out errorMessage);
            }

            try
            {
                HandleSingleControl(ctrl);
                return true;
            }
            catch (Exception ex)
            {
                errorCode = SceneApi.V2.SceneApiErrorCodes.CONSTRAINT_VIOLATION;
                errorMessage = ex.Message;
                return false;
            }
        }

        private void HandleControlBatch(ControlObject[] controlActions)
        {
            if (controlActions == null || controlActions.Length == 0)
            {
                Debug.LogWarning("[ControlManager] ⚠️ Received null or empty control actions array.");
                return;
            }

            ResolveSceneRegistry();
            bool openedBatch = sceneRegistry != null;
            if (openedBatch)
            {
                sceneRegistry.BeginMutationBatch("legacy_control_batch");
            }

            Debug.Log($"[ControlManager] 📦 Processing {controlActions.Length} control action(s)");

            try
            {
                for (int i = 0; i < controlActions.Length; i++)
                {
                    var ctrl = controlActions[i];
                    Debug.Log($"[ControlManager] 🎯 Processing action {i + 1}/{controlActions.Length}: target='{ctrl?.target}', action='{ctrl?.action}'");
                    HandleSingleControl(ctrl);
                }
            }
            finally
            {
                if (openedBatch)
                {
                    sceneRegistry.CommitMutationBatch();
                }
            }
        }

        private void HandleSingleControl(ControlObject ctrl)
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

            if (ctrl.action == "remove_sensor")
            {
                HandleRemoveSensorAction(ctrl);
                return;
            }

            if (ctrl.action == "delegate_task")
            {
                HandleDelegateTaskAction(ctrl);
                return;
            }

            if (IsAvatarAction(ctrl.action))
            {
                TryExecuteAvatarAction(ctrl, out _, out _);
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

        private void HandleControlWithProperties(GameObject targetObj, ObjectDescriber describer, ControlObject ctrl)
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

        private void HandleControlWithoutProperties(GameObject targetObj, ControlObject ctrl)
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

        private void HandleStateObjectControl(StateObject obj, ControlObject ctrl)
        {
            switch (ctrl.action)
            {
                case "set_state":
                    if (ctrl.parameters != null && ctrl.parameters.ContainsKey("state"))
                    {
                        string newState = ParseStringFromParameter(ctrl.parameters["state"]);
                        string previousState = obj.getCurrentState();
                        if (previousState == newState)
                        {
                            Debug.Log($"[ControlManager] ℹ️ State for '{obj.name}' already '{newState}', skipping mutation.");
                            return;
                        }

                        obj.setCurrentState(newState);
                        Debug.Log($"[ControlManager] ✅ Set '{obj.name}' to state '{newState}'");
                        RegisterMutation("control.local", obj.gameObject, ctrl.action);
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

        private void HandleTransformAction(GameObject obj, ControlObject ctrl)
        {
            Debug.Log($"[ControlManager] 🔄 Performing transform action on '{obj.name}'");
            
            if (ctrl.parameters == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ Transform action requires parameters.");
                return;
            }

            var before = SceneTransformSnapshot.Capture(obj.name, obj.transform);
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
                RegisterMutation("control.local", obj, ctrl.action);
                RecordTransformChange(
                    "agent",
                    ctrl.action,
                    obj.name,
                    before,
                    SceneTransformSnapshot.Capture(obj.name, obj.transform),
                    ctrl);
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

        private void HandleHighlightAction(GameObject obj, ControlObject ctrl)
        {
            Debug.Log($"[ControlManager] ✨ Highlighting '{obj.name}'");

            var describer = obj.GetComponent<ObjectDescriber>();
            if (describer == null)
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Object '{obj.name}' has no ObjectDescriber component.");
                return;
            }

            var targetRenderer = describer.GetObjectRenderer();
            if (targetRenderer == null)
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Cannot highlight '{obj.name}' - ObjectDescriber has no Renderer.");
                return;
            }

            // 1. 备份原始 sharedMaterials
            var originalMaterials = new List<Material>();
            targetRenderer.GetSharedMaterials(originalMaterials);

            // 2. 构造高亮用的材质列表（拷贝一份，不直接改 original）
            var highlightMaterials = new List<Material>(originalMaterials);
            if (!highlightMaterials.Contains(highlightMaterial))
            {
                highlightMaterials.Add(highlightMaterial);
            }

            // 3. 应用高亮材质
            targetRenderer.SetSharedMaterials(highlightMaterials);

            // 4. 延时恢复
            StartCoroutine(RestoreMaterialAfterDelay(targetRenderer, originalMaterials, 5.0f));
        }

        /// <summary>
        /// 在指定延迟后恢复物体的原始颜色
        /// </summary>
        /// <param name="renderer">渲染器组件</param>
        /// <param name="originalColor">原始颜色</param>
        /// <param name="delay">延迟时间（秒）</param>
        /// <param name="objectName">物体名称（用于日志）</param>
        private System.Collections.IEnumerator RestoreMaterialAfterDelay(Renderer renderer, List<Material> originalMaterials, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (renderer != null)
            {
                renderer.SetSharedMaterials(originalMaterials);
            }
        }

        // ===================== SENSOR CONTROL METHODS =====================

        [SerializeField] private VsensAgentSensorManager sensorManager;

        private void ResolveSensorManager()
        {
            if (sensorManager != null)
            {
                return;
            }

            sensorManager = VsensAgentSensorManager.Instance
                ?? (ServiceLocator.IsRegistered<VsensAgentSensorManager>()
                    ? ServiceLocator.Get<VsensAgentSensorManager>()
                    : FindFirstObjectByType<VsensAgentSensorManager>());
        }

        private void HandleRemoveSensorAction(ControlObject ctrl)
        {
            ResolveSensorManager();
            if (sensorManager == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ Cannot remove sensor: VsensAgentSensorManager not found.");
                return;
            }

            var sensorName = ctrl != null ? ctrl.target : string.Empty;
            if (!sensorManager.TryRemoveSensor(sensorName, out var error))
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Failed to remove sensor '{sensorName}': {error}");
                return;
            }

            ClearEditSelection(sensorName);
            RegisterMutation("control.local", sensorName, "remove_sensor");
            Debug.Log($"[ControlManager] 🗑️ Removed sensor: {sensorName}");
        }

        /// <summary>
        /// 处理传感器创建和修改命令
        /// </summary>
        /// <param name="ctrl">传感器控制命令</param>
        private void HandleSetSensorAction(ControlObject ctrl)
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
                SceneTransformSnapshot beforeTransform = null;
                if (!string.IsNullOrEmpty(ctrl.target))
                {
                    existingSensor = GameObject.Find(ctrl.target);
                    if (existingSensor != null)
                    {
                        Debug.Log($"[ControlManager] 🔄 Modifying existing sensor: {ctrl.target}");
                        beforeTransform = SceneTransformSnapshot.Capture(existingSensor.name, existingSensor.transform);
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
                        
                        // 尝试从服务定位器查找 VsensAgentSensorManager
                        var sensorManagerInScene = ServiceLocator.Get<VsensAgentSensorManager>();
                        if (sensorManagerInScene != null)
                        {
                            Debug.LogWarning("[ControlManager] 🔧 Found VsensAgentSensorManager via ServiceLocator but Instance is null. This suggests initialization issue.");
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
                bool transformChanged = false;
                bool sensorSpecificChanged = false;
                bool createdNewSensor = existingSensor == null;
                Debug.Log("[ControlManager] 🌍 Applying Agent's global transform parameters...");
                if (sensorObj != null)
                {
                    Debug.Log($"[ControlManager] 📍 sensorObj is valid: {sensorObj.name} (active: {sensorObj.activeInHierarchy})");
                    transformChanged = ApplyTransformParameters(sensorObj, ctrl.parameters);
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
                    sensorSpecificChanged = ApplySensorSpecificParameters(sensorObj, ctrl.parameters);

                    if (createdNewSensor || transformChanged || sensorSpecificChanged)
                    {
                        RegisterMutation("control.local", sensorObj, ctrl.action);
                    }

                    if (!createdNewSensor)
                    {
                        RecordTransformChange(
                            "agent",
                            ctrl.action,
                            sensorObj.name,
                            beforeTransform,
                            SceneTransformSnapshot.Capture(sensorObj.name, sensorObj.transform),
                            ctrl);
                    }
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

        private void HandleDelegateTaskAction(ControlObject ctrl)
        {
            string taskType = ParseStringParameter(ctrl.parameters, "task_type", "analysis");
            string goal = ParseStringParameter(ctrl.parameters, "goal", string.Empty);
            string selector = ParseStringParameter(ctrl.parameters, "selector", "recent_n");
            string timestampLabel = ParseStringParameter(ctrl.parameters, "timestamp_label", null);
            int recentN = 3;
            if (ctrl.parameters != null && ctrl.parameters.TryGetValue("recent_n", out var recentNValue) && recentNValue != null)
            {
                int.TryParse(recentNValue.ToString(), out recentN);
                if (recentN <= 0)
                {
                    recentN = 3;
                }
            }

            Debug.Log($"[ControlManager] 📈 Forwarding delegate_task request task_type='{taskType}', selector='{selector}', recent_n={recentN}, timestamp='{timestampLabel}'");
            WsClient.SendDelegatedTaskStart(
                taskType: string.IsNullOrWhiteSpace(taskType) ? "analysis" : taskType,
                goal: goal,
                selector: string.IsNullOrWhiteSpace(selector) ? "recent_n" : selector,
                recentN: recentN,
                timestampLabel: string.IsNullOrWhiteSpace(timestampLabel) ? null : timestampLabel);
        }

        /// <summary>
        /// 应用传感器特定的参数
        /// </summary>
        /// <param name="sensorObj">传感器游戏对象</param>
        /// <param name="parameters">参数字典</param>
        private bool ApplySensorSpecificParameters(GameObject sensorObj, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            if (sensorObj == null)
            {
                Debug.LogError("[ControlManager] ❌ ApplySensorSpecificParameters: sensorObj is null");
                return false;
            }
            
            if (parameters == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ ApplySensorSpecificParameters: parameters is null");
                return false;
            }

            Debug.Log($"[ControlManager] 🔧 Applying sensor-specific parameters to {sensorObj.name}");
            bool changed = false;

            // 获取SensorObjectDescriber组件
            SensorObjectDescriber sensorDescriber = sensorObj.GetComponent<SensorObjectDescriber>();
            if (sensorDescriber == null)
            {
                Debug.LogWarning($"[ControlManager] ⚠️ No SensorObjectDescriber found on {sensorObj.name}");
                return false;
            }

            // 处理show_visualization参数
            if (parameters.ContainsKey("show_visualization"))
            {
                string showVisString = ParseStringFromParameter(parameters["show_visualization"]);
                bool showVis = bool.Parse(showVisString);
                Debug.Log($"[ControlManager] 👁️ Setting show_visualization to {showVis} for {sensorObj.name}");
                // 这里可以添加具体的可视化控制逻辑
                if (sensorDescriber.Sesnor.ShowPreview != showVis)
                {
                    changed = true;
                }
                sensorDescriber.Sesnor.ShowPreview = showVis;
            }

            // 处理show_data_graph参数
            if (parameters.ContainsKey("show_data_graph"))
            {
                string showDataString = ParseStringFromParameter(parameters["show_data_graph"]);
                bool showData = bool.Parse(showDataString);
                Debug.Log($"[ControlManager] 📊 Setting show_data_graph to {showData} for {sensorObj.name}");
                // 这里可以添加具体的数据图表控制逻辑
                if (sensorDescriber.Sesnor.ShowGraph != showData)
                {
                    changed = true;
                }
                sensorDescriber.Sesnor.ShowGraph = showData;
            }

            // 处理validDistance参数 (仅用于DISTANCE传感器)
            if (parameters.ContainsKey("validDistance"))
            {
                string distanceString = ParseStringFromParameter(parameters["validDistance"]);
                float distance = float.Parse(distanceString);
                Debug.Log($"[ControlManager] 📏 Setting validDistance to {distance} for {sensorObj.name}");
                // 这里可以添加具体的距离设置逻辑
                if (sensorDescriber.Sesnor is VirtualDistanceSensor distanceSensor)
                {
                    if (!Mathf.Approximately(distanceSensor.validDistance, distance))
                    {
                        changed = true;
                    }
                    distanceSensor.validDistance = distance;
                }
            }

            // 处理lookDirection参数 (仅用于DISTANCE传感器)
            if (parameters.ContainsKey("lookDirection"))
            {
                Vector3 lookDir = ParseVector3FromArray(parameters["lookDirection"], Vector3.forward);
                Debug.Log($"[ControlManager] 👀 Setting lookDirection to {lookDir} for {sensorObj.name}");
                // 应用朝向
                if (lookDir != Vector3.zero)
                {
                    Quaternion newRotation = Quaternion.LookRotation(lookDir);
                    if (sensorObj.transform.rotation != newRotation)
                    {
                        changed = true;
                        sensorObj.transform.rotation = newRotation;
                    }
                }
            }

            // 处理parent参数 (重要：必须在应用global坐标之后设置)
            // Agent给出的坐标是global的，我们先应用了这些坐标，现在设置parent让Unity自动转换为local坐标
            if (TryApplyAvatarJointAttachment(sensorObj, parameters))
            {
                changed = true;
            }
            else
            if (parameters.ContainsKey("parent"))
            {
                string parentName = ParseStringFromParameter(parameters["parent"]);
                if (!string.IsNullOrEmpty(parentName))
                {
                    GameObject parentObj = GameObject.Find(parentName);
                    if (parentObj != null)
                    {
                        if (sensorObj.transform.parent != parentObj.transform)
                        {
                            changed = true;
                        }
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
                    if (sensorObj.transform.parent != null)
                    {
                        changed = true;
                    }
                    Debug.Log($"[ControlManager] 🆓 Removing parent from {sensorObj.name} (back to global space)");
                    sensorObj.transform.SetParent(null);
                }
            }

            return changed;
        }

        private bool TryApplyAvatarJointAttachment(GameObject sensorObj, Dictionary<string, object> parameters)
        {
            if (!IsAvatarJointAttach(parameters))
            {
                return false;
            }

            ResolveAvatarRuntimeManager();
            if (avatarRuntimeManager == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ Cannot attach sensor to avatar joint because AvatarRuntimeManager was not found.");
                return false;
            }

            var avatarId = ParseStringFromParameter(parameters["avatar_id"]);
            var jointName = ParseStringFromParameter(parameters["joint_name"]);
            if (!avatarRuntimeManager.TryResolveAttachmentPointTransform(avatarId, jointName, out var attachmentTransform, out var error))
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Failed to resolve avatar attachment point '{jointName}' on '{avatarId}': {error}");
                return false;
            }

            sensorObj.transform.SetParent(attachmentTransform, false);
            if (parameters.ContainsKey("local_position"))
            {
                sensorObj.transform.localPosition = ParseVector3FromArray(parameters["local_position"], Vector3.zero);
            }
            else
            {
                sensorObj.transform.localPosition = Vector3.zero;
            }

            if (parameters.ContainsKey("local_rotation"))
            {
                sensorObj.transform.localRotation = Quaternion.Euler(ParseVector3FromArray(parameters["local_rotation"], Vector3.zero));
            }
            else
            {
                sensorObj.transform.localRotation = Quaternion.identity;
            }

            Debug.Log($"[ControlManager] 🤝 Attached sensor '{sensorObj.name}' to avatar joint '{jointName}' on '{avatarId}'.");
            return true;
        }

        private static bool IsAvatarJointAttach(Dictionary<string, object> parameters)
        {
            if (parameters == null || !parameters.TryGetValue("attach_mode", out var attachModeObj) || attachModeObj == null)
            {
                return false;
            }

            return string.Equals(
                attachModeObj.ToString(),
                "avatar_joint",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 应用Agent给出的global变换参数到游戏对象
        /// 注意：Agent给出的所有坐标和旋转都是global coordinate，必须在设置parent之前应用
        /// </summary>
        /// <param name="obj">目标游戏对象</param>
        /// <param name="parameters">参数字典</param>
        private bool ApplyTransformParameters(GameObject obj, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            if (obj == null)
            {
                Debug.LogError("[ControlManager] ❌ ApplyTransformParameters: obj is null");
                return false;
            }
            
            if (parameters == null)
            {
                Debug.LogWarning("[ControlManager] ⚠️ ApplyTransformParameters: parameters is null");
                return false;
            }

            Debug.Log($"[ControlManager] 🌍 Applying Agent's global transform parameters to {obj.name}");
            bool changed = false;

            // 处理位置参数 - Agent给出的是global坐标
            if (parameters.ContainsKey("position"))
            {
                Vector3 newPosition = ParseVector3FromArray(parameters["position"], obj.transform.position);
                if (newPosition != obj.transform.position)
                {
                    obj.transform.position = newPosition;
                    Debug.Log($"[ControlManager] 📍 Set global position of '{obj.name}' to {newPosition}");
                    changed = true;
                }
            }

            // 处理旋转参数 - Agent给出的是global旋转
            if (parameters.ContainsKey("rotation"))
            {
                Vector3 newRotation = ParseVector3FromArray(parameters["rotation"], obj.transform.eulerAngles);
                Quaternion rotation = Quaternion.Euler(newRotation);
                if (rotation != obj.transform.rotation)
                {
                    obj.transform.rotation = rotation;
                    Debug.Log($"[ControlManager] 🔄 Set global rotation of '{obj.name}' to {newRotation}");
                    changed = true;
                }
            }

            // 注意：根据用户反馈，删除了scale参数支持，因为用户不太会去调整传感器的大小
            return changed;
        }

        private void ResolveSceneRegistry()
        {
            if (sceneRegistry != null)
            {
                return;
            }

            if (ServiceLocator.IsRegistered<SceneRegistry>())
            {
                sceneRegistry = ServiceLocator.Get<SceneRegistry>();
            }
            if (sceneRegistry == null)
            {
                sceneRegistry = FindFirstObjectByType<SceneRegistry>();
            }
        }

        private void ResolveAvatarRuntimeManager()
        {
            if (avatarRuntimeManager != null)
            {
                return;
            }

            if (ServiceLocator.IsRegistered<AvatarRuntimeManager>())
            {
                avatarRuntimeManager = ServiceLocator.Get<AvatarRuntimeManager>();
            }
            if (avatarRuntimeManager == null)
            {
                avatarRuntimeManager = FindFirstObjectByType<AvatarRuntimeManager>();
            }
        }

        private void ResolveSceneActionHistory()
        {
            if (sceneActionHistory != null)
            {
                return;
            }

            sceneActionHistory = SceneActionHistory.GetOrCreate();
        }

        private void RecordTransformChange(
            string source,
            string actionType,
            string targetId,
            SceneTransformSnapshot before,
            SceneTransformSnapshot after,
            ControlObject ctrl)
        {
            ResolveSceneActionHistory();
            if (sceneActionHistory == null)
            {
                return;
            }

            var rawJson = ctrl != null ? JsonConvert.SerializeObject(ctrl) : null;
            sceneActionHistory.TryRecordTransformChange(source, actionType, targetId, before, after, rawJson);
        }

        private void RegisterMutation(string source, GameObject target, string actionType)
        {
            RegisterMutation(source, target != null ? target.name : string.Empty, actionType);
        }

        private void RegisterMutation(string source, string targetId, string actionType)
        {
            ResolveSceneRegistry();
            if (sceneRegistry == null)
            {
                return;
            }

            sceneRegistry.RegisterMutation(source, targetId, actionType);
        }

        private void ClearEditSelection(string objectId)
        {
            var editController = ServiceLocator.IsRegistered<RuntimeEditModeController>()
                ? ServiceLocator.Get<RuntimeEditModeController>()
                : FindFirstObjectByType<RuntimeEditModeController>();
            editController?.ClearSelectionIfSelected(objectId);
        }

        private static bool IsAvatarAction(string action)
        {
            switch (action)
            {
                case "spawn_avatar":
                case "remove_avatar":
                case "set_avatar_transform":
                case "load_avatar_motion":
                case "play_avatar_motion":
                case "pause_avatar_motion":
                case "stop_avatar_motion":
                case "clear_avatar_motion":
                    return true;
                default:
                    return false;
            }
        }

        private bool TryValidateAvatarAction(ControlObject ctrl, out string errorCode, out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;

            ResolveAvatarRuntimeManager();
            if (avatarRuntimeManager == null)
            {
                errorCode = SceneApi.V2.SceneApiErrorCodes.CONSTRAINT_VIOLATION;
                errorMessage = "AvatarRuntimeManager not found.";
                return false;
            }

            switch (ctrl.action)
            {
                case "spawn_avatar":
                    if (ctrl.parameters == null || !ctrl.parameters.ContainsKey("prefab_key"))
                    {
                        errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                        errorMessage = "spawn_avatar requires 'prefab_key'.";
                        return false;
                    }
                    return true;

                case "set_avatar_transform":
                    if (ctrl.parameters == null ||
                        (!ctrl.parameters.ContainsKey("position") &&
                         !ctrl.parameters.ContainsKey("rotation") &&
                         !ctrl.parameters.ContainsKey("scale")))
                    {
                        errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                        errorMessage = "set_avatar_transform requires one of position/rotation/scale.";
                        return false;
                    }
                    return true;

                case "load_avatar_motion":
                    if (ctrl.parameters == null || !ctrl.parameters.ContainsKey("motion_json"))
                    {
                        errorCode = SceneApi.V2.SceneApiErrorCodes.INVALID_PARAM;
                        errorMessage = "load_avatar_motion requires 'motion_json'.";
                        return false;
                    }
                    return true;

                case "remove_avatar":
                case "play_avatar_motion":
                case "pause_avatar_motion":
                case "stop_avatar_motion":
                case "clear_avatar_motion":
                    return true;

                default:
                    errorCode = SceneApi.V2.SceneApiErrorCodes.UNSUPPORTED_ACTION;
                    errorMessage = $"Unsupported avatar action '{ctrl.action}'.";
                    return false;
            }
        }

        private bool TryExecuteAvatarAction(ControlObject ctrl, out string errorCode, out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;

            ResolveAvatarRuntimeManager();
            if (avatarRuntimeManager == null)
            {
                errorCode = SceneApi.V2.SceneApiErrorCodes.CONSTRAINT_VIOLATION;
                errorMessage = "AvatarRuntimeManager not found.";
                return false;
            }

            string avatarId = string.IsNullOrWhiteSpace(ctrl.target) ? "avatar_main" : ctrl.target;
            bool ok;

            switch (ctrl.action)
            {
                case "spawn_avatar":
                    ok = avatarRuntimeManager.TrySpawnAvatar(
                        avatarId,
                        ParseStringParameter(ctrl.parameters, "prefab_key", "smplx_male"),
                        ParseVector3Parameter(ctrl.parameters, "position", Vector3.zero),
                        ParseVector3Parameter(ctrl.parameters, "rotation", Vector3.zero),
                        out errorMessage
                    );
                    if (ok)
                    {
                        RegisterMutation("control.local", avatarId, ctrl.action);
                    }
                    break;

                case "remove_avatar":
                    ok = avatarRuntimeManager.TryRemoveAvatar(avatarId, out errorMessage);
                    if (ok)
                    {
                        ClearEditSelection(avatarId);
                        RegisterMutation("control.local", avatarId, ctrl.action);
                    }
                    break;

                case "set_avatar_transform":
                    var avatarObjectBefore = avatarRuntimeManager.GetManagedAvatarObject();
                    var beforeTransform = SceneTransformSnapshot.Capture(avatarId, avatarObjectBefore != null ? avatarObjectBefore.transform : null);
                    ok = avatarRuntimeManager.TrySetAvatarTransform(
                        avatarId,
                        TryParseVector3Parameter(ctrl.parameters, "position"),
                        TryParseVector3Parameter(ctrl.parameters, "rotation"),
                        TryParseVector3Parameter(ctrl.parameters, "scale"),
                        out errorMessage
                    );
                    if (ok)
                    {
                        RegisterMutation("control.local", avatarId, ctrl.action);
                        var avatarObjectAfter = avatarRuntimeManager.GetManagedAvatarObject();
                        RecordTransformChange(
                            "agent",
                            ctrl.action,
                            avatarId,
                            beforeTransform,
                            SceneTransformSnapshot.Capture(avatarId, avatarObjectAfter != null ? avatarObjectAfter.transform : null),
                            ctrl);
                    }
                    break;

                case "load_avatar_motion":
                    ok = avatarRuntimeManager.TryLoadAvatarMotion(
                        avatarId,
                        ParseStringParameter(ctrl.parameters, "motion_id", "motion_inline"),
                        ParseStringParameter(ctrl.parameters, "motion_name", "avatar_motion"),
                        ParseStringParameter(ctrl.parameters, "motion_json", string.Empty),
                        ParseStringParameter(ctrl.parameters, "source_text", string.Empty),
                        out errorMessage
                    );
                    if (ok)
                    {
                        RegisterMutation("control.local", avatarId, ctrl.action);
                    }
                    break;

                case "play_avatar_motion":
                    ok = avatarRuntimeManager.TryPlayAvatarMotion(
                        avatarId,
                        ParseFloatParameter(ctrl.parameters, "speed", 1f),
                        ParseBoolParameter(ctrl.parameters, "loop", true),
                        out errorMessage
                    );
                    if (ok)
                    {
                        RegisterMutation("control.local", avatarId, ctrl.action);
                    }
                    break;

                case "pause_avatar_motion":
                    ok = avatarRuntimeManager.TryPauseAvatarMotion(avatarId, out errorMessage);
                    if (ok)
                    {
                        RegisterMutation("control.local", avatarId, ctrl.action);
                    }
                    break;

                case "stop_avatar_motion":
                    ok = avatarRuntimeManager.TryStopAvatarMotion(avatarId, out errorMessage);
                    if (ok)
                    {
                        RegisterMutation("control.local", avatarId, ctrl.action);
                    }
                    break;

                case "clear_avatar_motion":
                    ok = avatarRuntimeManager.TryClearAvatarMotion(avatarId, out errorMessage);
                    if (ok)
                    {
                        RegisterMutation("control.local", avatarId, ctrl.action);
                    }
                    break;

                default:
                    ok = false;
                    errorMessage = $"Unsupported avatar action '{ctrl.action}'.";
                    break;
            }

            if (!ok)
            {
                errorCode = SceneApi.V2.SceneApiErrorCodes.CONSTRAINT_VIOLATION;
            }

            return ok;
        }

        private Vector3 ParseVector3Parameter(Dictionary<string, object> parameters, string key, Vector3 defaultValue)
        {
            if (parameters == null || !parameters.ContainsKey(key))
            {
                return defaultValue;
            }

            return ParseVector3FromAny(parameters[key], defaultValue);
        }

        private Vector3? TryParseVector3Parameter(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.ContainsKey(key))
            {
                return null;
            }

            return ParseVector3FromAny(parameters[key], Vector3.zero);
        }

        private float ParseFloatParameter(Dictionary<string, object> parameters, string key, float defaultValue)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
            {
                return defaultValue;
            }

            if (value is float floatValue) return floatValue;
            if (value is double doubleValue) return (float)doubleValue;
            if (value is int intValue) return intValue;
            if (float.TryParse(value.ToString(), out var parsed)) return parsed;
            return defaultValue;
        }

        private bool ParseBoolParameter(Dictionary<string, object> parameters, string key, bool defaultValue)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
            {
                return defaultValue;
            }

            if (value is bool boolValue) return boolValue;
            if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
            return defaultValue;
        }

        private string ParseStringParameter(Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value) || value == null)
            {
                return defaultValue;
            }

            return value.ToString();
        }

        private Vector3 ParseVector3FromAny(object paramValue, Vector3 defaultValue)
        {
            if (paramValue is float[] floatArray && floatArray.Length >= 3)
            {
                return new Vector3(floatArray[0], floatArray[1], floatArray[2]);
            }
            if (paramValue is int[] intArray && intArray.Length >= 3)
            {
                return new Vector3(intArray[0], intArray[1], intArray[2]);
            }
            if (paramValue is object[] objectArray && objectArray.Length >= 3)
            {
                return new Vector3(
                    float.Parse(objectArray[0].ToString()),
                    float.Parse(objectArray[1].ToString()),
                    float.Parse(objectArray[2].ToString())
                );
            }

            return ParseVector3FromArray(paramValue, defaultValue);
        }

        // ===================== END SENSOR CONTROL METHODS =====================
    }
}
