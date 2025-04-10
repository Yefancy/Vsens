using System;
using UnityEngine;

namespace Sensor
{
    public class SensorSpawner : MonoBehaviour
    {
        public VirtualSensor prefab;
        
        // runtime
        private VirtualSensor _sensorPreview;
        
        private void Start()
        {
            SetPrefab(prefab);
        }
        
        public void SetPrefab(VirtualSensor prefab)
        {
            this.prefab = prefab;
            if (_sensorPreview != null)
            {
                Destroy(_sensorPreview.gameObject);
            }
            SpawnPreview();
        }
        
        private void SpawnPreview()
        {
            if (prefab == null) return;
            _sensorPreview = Instantiate(prefab, transform);
            _sensorPreview.showSelectedVisualization = false;
            _sensorPreview.registerOnStart = false;
            _sensorPreview.canSelected = false;
            _sensorPreview.prefab = prefab.gameObject;
            _sensorPreview.gameObject.SetActive(true);
            _sensorPreview.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            _sensorPreview.transform.localPosition = new Vector3(0, 0.015f, 0);
        }
        
        private void Update()
        {
            // sensor is move away from the spawner
            if (_sensorPreview != null && Vector3.Distance(_sensorPreview.transform.position, transform.position) > 0.2f)
            {
                _sensorPreview.showSelectedVisualization = true;
                _sensorPreview.canSelected = true;
                SpawnPreview();
            }
        }
    }
}
