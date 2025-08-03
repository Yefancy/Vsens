using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Sensor;
using UnityEngine;

namespace VsensAgent.VirtualObject.Sensor
{
    public class VsensAgentSensorManager : MonoBehaviour
    {
        public static VsensAgentSensorManager Instance { get; private set; }
        
        [SerializeField] private List<VirtualSensor> _registeredSensors = new List<VirtualSensor>();   
        
        public VsensAgentSensorManager()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple instances of SensorHelper detected. Using the first instance.");
            }
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
            foreach (var prefab in _registeredSensors)
            {
                if (prefab.SensorDefinition().getSensorName() != sensorName) continue;
                var created = Instantiate(prefab, parent);
                created.prefab = prefab.gameObject;
                return created;
            }
            return null;
        }
    }
}