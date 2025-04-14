using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sensor;
using smplx;
using UnityEditor;
using UnityEngine;
using Vsens.data;

namespace Scenes.UserStudy
{
    public class US2_Data_Inspector : MonoBehaviour
    {
        public IMUChart rIMU;
        public IMUChart vIMU;
        public SmplxBodyAnimationController controller;
        public bool isAcc = true;
        public VirtualIMUSensor[] imuSensors;
        public List<List<SensorData>> imuData = new();
        public string userName = "";
        public string expName = "";
        public bool saveWithID = true;
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(US2_Data_Inspector))]
    public class US2_Data_InspectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var inspector = (US2_Data_Inspector)target;
            if (GUILayout.Button("Find IMU"))
            {
                inspector.imuSensors = FindObjectsOfType<VirtualIMUSensor>().Where(sensor => sensor.gameObject.activeInHierarchy).ToArray();
            }
            var controller = inspector.controller;
            var imuSensors = inspector.imuSensors;
            if (imuSensors.Length > 0 && GUILayout.Button("Synthesize IMU"))
            {
                inspector.imuData.Clear();
                var smplx = controller.GetComponent<SMPLX>();
                // simulate the virtual imu
                controller.PlayAnimationTo(0);
                foreach (var sensor in imuSensors)
                {
                    sensor.ClearData();
                    sensor.ClearSmoothCache();
                    sensor.StartRecording();
                }
                var time = 0f;
                var deltaTime = controller.deltaTime;
                var animationTime = controller.frameCount * controller.deltaTime;
                while (time < animationTime)
                {
                    controller.PlayAnimationTo(time);
                    if (smplx.usePoseCorrectives)
                    {
                        smplx.UpdatePoseCorrectives();
                    }
                    foreach (var sensor in imuSensors)
                    {
                        sensor.UpdateWorking(time, deltaTime);
                    }
                    time += deltaTime;
                }
                foreach (var sensor in imuSensors)
                {
                    sensor.StopRecording();
                    inspector.imuData.Add(sensor.Data);
                }
                controller.PlayAnimationToTime();
                // visualization
                if (inspector.vIMU != null)
                {
                    inspector.vIMU.IsAccMode = inspector.isAcc;
                    inspector.vIMU.updateIMUData(inspector.imuData[0]);
                }
            }

            if (inspector.imuData.Count > 0 && GUILayout.Button("Save IMU Data"))
            {
                var fileName = inspector.saveWithID ? inspector.userName + "_" + inspector.expName : "imu_data";
                // save the data as csv
                var path = EditorUtility.SaveFilePanel("Save IMU Data", "", fileName, "csv");
                if (path.Length != 0)
                {
                    using var writer = new StreamWriter(path);
                    writer.WriteLine("tag,time,ex,ey,ez,ax,ay,az,lx,ly,lz,x,y,z");
                    foreach (var data in inspector.imuData)
                    {
                        // data is a list of SensorData
                        foreach (var sensorData in data)
                        {
                            writer.WriteLine(sensorData.ToCsvLine());
                        }
                    }
                } 
            }
        }
    }
#endif
}

