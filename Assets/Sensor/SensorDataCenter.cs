using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Animations;
using Scenes.Replay.sensor;
using UnityEngine;
using XCharts.Runtime;

namespace Sensor
{
    public class SensorDataCenter : MonoBehaviour
    {
        private static int AUTOMATIC_ID;

        private static SensorDataCenter INSTANCE;
        public static SensorDataCenter Instance => INSTANCE;
    
        [SerializeField] public int samplingRate = 20; // 20Hz 
        [SerializeField] public float smoothWindowSize = 0.2f;
        [SerializeField] private int cacheTime = 4;
        [SerializeField] public bool alwaysUpdateBoneMeshAttachment = true;

        public float SamplingInterval => 1.0f / samplingRate;
        // run-time
        private readonly Dictionary<ISensorDefinition, List<VirtualSensor>> sensors = new();
        private readonly List<Tuple<float, Vector3>> accelerationData = new();
        private readonly List<Tuple<float, Vector3>> orientationData = new();
        private bool isSimulating;
        public bool IsSimulating => isSimulating;
        private bool isRecording;
        public bool IsRecording => isRecording;

        public void Start()
        {
            if (INSTANCE != null)
            {
                Debug.LogError("There are multiple SensorDataCollector in the scene.");
            }
            INSTANCE = this;
        }
        
        public void RegisterSensor(VirtualSensor sensor)
        {
            if (sensors.TryGetValue(sensor.SensorDefinition(), out var sensorList))
            {
                sensor.name = $"{sensor.SensorDefinition().getSensorName()}-{AUTOMATIC_ID++}";
                sensorList.Add(sensor);
            }
            else
            {
                sensors[sensor.SensorDefinition()] = new List<VirtualSensor> {sensor};
            }
        }
        
        public void UnregisterSensor(VirtualSensor sensor)
        {
            if (sensors.TryGetValue(sensor.SensorDefinition(), out var sensorList))
            {
                sensorList.Remove(sensor);
            }
        }
        
        public void StartRecording()
        {
            isRecording = true;
            sensors.Values.ToList().ForEach(sensorList => sensorList.ForEach(sensor =>
            {
                sensor.ClearData();
                sensor.ClearSmoothCache();
                sensor.StartRecording();
            }));
        }
        
        public Dictionary<VirtualSensor, List<SensorData>> StopRecording()
        {
            isRecording = false;
            var result = new Dictionary<VirtualSensor, List<SensorData>>();
            foreach (var sensorDataPair in sensors)
            {
                foreach (var sensor in sensorDataPair.Value)
                {
                    sensor.StopRecording();
                    var sensorData = sensor.Data;
                    if (sensorData.Count == 0) continue;
                    result[sensor] = sensorData;
                }
                // var sensorData =  sensorDataPair.Value.SelectMany(sensor => sensor.Data).OrderBy(data => data.time).ToList();
                // if (sensorData.Count == 0) continue;
                // // save results
                // var rootPath = Application.dataPath + "/../Data";
                // if (!Directory.Exists(rootPath))
                // {
                //     Directory.CreateDirectory(rootPath);
                // }
                // var path = $"{rootPath}/{sensorDataPair.Key.getSensorName().ToLower()}-{name}.csv";
                // using var writer = new StreamWriter(path);
                // writer.WriteLine($"tag,time,{sensorDataPair.Key.getCsvHeader()}");
                // foreach (var data in sensorData) {
                //     writer.WriteLine(data.ToCsvLine());
                // }
            }
            return result;
        }

        public Dictionary<ISensorDefinition, List<SensorData>> SimulateSensorData(IAnimationRuntime animationRuntime, SimulationConfig config)
        {
            isSimulating = true;
            var sensors = animationRuntime.getGameObject().GetComponentsInChildren<VirtualSensor>();
            // prepare for simulation
            foreach (var sensor in sensors)
            {
                sensor.ClearData();
                sensor.ClearSmoothCache();
                sensor.StartRecording();
            }
        
            // simulation
            var animationController = animationRuntime.getAnimationController();
            var lastSpeed = animationController.speed;
            var lastTime = animationController.normalizedTime;
            var lastSamplingRate = samplingRate;
            var lastSmooth = smoothWindowSize;
            var lastAmplitude = animationController.amplitude;
            var lastInitialMotionBones = animationController.GetInitialMotionPose;
        
            var time = 0f;
            var deltaTime = config.deltaTime;
            samplingRate = config.samplingRate;
            smoothWindowSize = config.smoothWindowSize;
            animationController.normalizedTime = config.startTime;
            animationController.speed = config.speed;
            animationController.amplitude = config.amplitude;
            animationController.SetInitialMotionPose(animationController.getPose(config.startTime, true));
            animationRuntime.SetBodyShape(config.bodyShapes);
        
            var currentTime = animationController.normalizedTime;
            while (currentTime < config.endTime)
            {
                time += deltaTime;
                animationController.RunNextFrame(deltaTime);
                foreach (var sensor in sensors)
                {
                    sensor.UpdateWorking(time, deltaTime);
                }
                var normalizedTime = animationController.normalizedTime;
                if (normalizedTime < currentTime)
                {
                    break;
                }
                currentTime = normalizedTime;
            }
        
            animationController.speed = lastSpeed;
            animationController.normalizedTime = lastTime;
            animationController.amplitude = lastAmplitude;
            animationController.SetInitialMotionPose(lastInitialMotionBones);
            samplingRate = lastSamplingRate;
            smoothWindowSize = lastSmooth;

            Dictionary<ISensorDefinition, List<SensorData>> results = new(); 
        
            foreach (var sensorGroup in sensors.GroupBy(sensor => sensor.SensorDefinition()))
            {
                var senorData = sensorGroup.SelectMany(sensor => sensor.Data).OrderBy(data => data.time).ToList();
                if (senorData.Count == 0) continue;
                results[sensorGroup.Key] = senorData;
            }
        
            foreach (var sensor in sensors)
            {
                sensor.StopRecording();
            }
            isSimulating = false;
        
            return results;
        }
    }
}
