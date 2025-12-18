using System.Linq;
using Sensor;
using SimpleJSON;
using UnityEditor;
using UnityEngine;

namespace Scenes.UserStudy
{
    public class US1_DataCollector : MonoBehaviour
    {
        private SMPLX smplx;
        public VirtualIMUSensor[] imuSensors;
        public string userName = "";
        public string expName = "";

        private void Awake()
        {
            smplx = GetComponentInChildren<SMPLX>();
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(US1_DataCollector))]
    public class US1_DataCollectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var userStudy = (US1_DataCollector)target;
            base.OnInspectorGUI();
            // button to detech all IMU sensors in the scene
            if (GUILayout.Button("Detect IMU Sensors"))
            {
                userStudy.imuSensors = FindObjectsByType<VirtualIMUSensor>(FindObjectsSortMode.None).Where(imu => imu.canSelected).ToArray();
                // move the IMU sensors to the same parent as this component
                foreach (var sensor in userStudy.imuSensors)
                {
                    sensor.transform.SetParent(userStudy.transform);
                }
            }
            if (GUILayout.Button("SaveData"))
            {
                var json = new JSONObject();
                json["user"] = userStudy.userName;
                json["exp"] = userStudy.expName;
                foreach (var sensor in userStudy.imuSensors)
                {
                    var position = sensor.transform.localPosition;
                    var rotation = sensor.transform.localRotation.eulerAngles;
                    var imuData = new JSONObject();
                    imuData["position"] = new JSONArray();
                    imuData["position"].Add(position.x);
                    imuData["position"].Add(position.y);
                    imuData["position"].Add(position.z);
                    imuData["rotation"] = new JSONArray();
                    imuData["rotation"].Add(rotation.x);
                    imuData["rotation"].Add(rotation.y);
                    imuData["rotation"].Add(rotation.z);
                    json[sensor.name] = imuData;
                }
                // open file save dialog
                var path = EditorUtility.SaveFilePanel("Save IMU Data", "", 
                    userStudy.userName + "_" + userStudy.expName, "json");
                if (path.Length != 0)
                {
                    // pretty print the json
                    var jsonString = json.ToString(2);
                    System.IO.File.WriteAllText(path, jsonString);
                    Debug.Log("Saved IMU Data to " + path);
                }
            }
        }
    }
#endif
}

