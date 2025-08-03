using UnityEngine;
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            if (ctrl == null || string.IsNullOrEmpty(ctrl.target))
            {
                Debug.LogWarning("[ControlManager] ⚠️ Received null or invalid control command.");
                return;
            }

            Debug.Log($"[ControlManager] 🎮 Handling control: {ctrl.action} on {ctrl.target}");

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
                // 简单的高亮实现 - 可以替换为更复杂的效果
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
                
                renderer.material.color = highlightColor;
                Debug.Log($"[ControlManager] ✅ Highlighted '{obj.name}' with color {highlightColor}");
            }
            else
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Cannot highlight '{obj.name}' - no Renderer component found.");
            }
        }
    }
}
