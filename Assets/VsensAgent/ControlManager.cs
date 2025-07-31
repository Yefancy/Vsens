using UnityEngine;
using System;

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

            // 2. 先尝试当作 StateObject
            StateObject stateObj = targetObj.GetComponent<StateObject>();
            if (stateObj != null)
            {
                HandleStateObjectControl(stateObj, ctrl);
                return;
            }

            // 3. 如果是 SensorObject（未来扩展）
            // SensorObject sensorObj = targetObj.GetComponent<SensorObject>();
            // if (sensorObj != null)
            // {
            //     HandleSensorObjectControl(sensorObj, ctrl);
            //     return;
            // }

            // 4. 如果啥都不是，可能是Highlight等其他操作
            // if (ctrl.action == "highlight")
            // {
            //     HighlightObject(targetObj);
            // }

            else
            {
                Debug.LogWarning($"[ControlManager] ⚠️ Target '{ctrl.target}' has no supported components for action '{ctrl.action}'.");
            }
        }

        private void HandleStateObjectControl(StateObject obj, WsClient.ControlObject ctrl)
        {
            switch (ctrl.action)
            {
                case "set_state":
                    if (ctrl.parameters != null && ctrl.parameters.ContainsKey("state"))
                    {
                        string newState = ctrl.parameters["state"];
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

        // private void HandleSensorObjectControl(SensorObject obj, WsClient.ControlObject ctrl)
        // {
        //     // 未来扩展：例如设置采样频率、模拟触发事件等
        //     switch (ctrl.action)
        //     {
        //         case "set_parameter":
        //             if (ctrl.parameters != null)
        //             {
        //                 foreach (var kv in ctrl.parameters)
        //                 {
        //                     obj.SetParameter(kv.Key, kv.Value);
        //                 }
        //                 Debug.Log($"[ControlManager] ✅ Updated sensor parameters for '{obj.name}'");
        //             }
        //             break;

        //         default:
        //             Debug.LogWarning($"[ControlManager] ⚠️ Unsupported action '{ctrl.action}' for SensorObject.");
        //             break;
        //     }
        // }

        // private void HighlightObject(GameObject obj)
        // {
        //     // 示例：用简单方式改变颜色，可替换成Shader闪烁或Outline
        //     Renderer rend = obj.GetComponent<Renderer>();
        //     if (rend != null)
        //     {
        //         rend.material.color = Color.yellow;
        //         Debug.Log($"[ControlManager] ✨ Highlighted {obj.name}");
        //     }
        // }
    }
}
