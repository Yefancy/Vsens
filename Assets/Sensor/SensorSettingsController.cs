using UnityEngine;
using UnityEngine.UI;

namespace Sensor
{
    public class SensorSettingsController : MonoBehaviour
    {
        [SerializeField] private VirtualSensor sensor;
        [SerializeField] private Transform center;

        [SerializeField] private Toggle activeToggle;
        [SerializeField] private Toggle previewToggle;
        [SerializeField] private Toggle graphToggle;
        
        //runtime
        private float radius;

        private void Start()
        {
            if (center == null)
            {
                center = sensor.gameObject.transform;
            }
            radius = (center.position - transform.position).magnitude;
            // toggles
            UpdateToggles();
            activeToggle.onValueChanged.AddListener(isOn => sensor.IsActive = !isOn);
            previewToggle.onValueChanged.AddListener(isOn => sensor.ShowPreview = isOn);
            graphToggle.onValueChanged.AddListener(isOn => sensor.ShowGraph = isOn);
        }
        
        public void RemoveSensor()
        {
            SensorDataCenter.Instance.UnregisterSensor(sensor);
            Destroy(sensor.gameObject);
        }
        
        private void UpdateToggles()
        {
            activeToggle.isOn = !sensor.IsActive;
            previewToggle.isOn = sensor.ShowPreview;
            graphToggle.isOn = sensor.ShowGraph;
        }

        private void Update()
        {
            if (!(radius > 0)) return;
            var centerPosition = center.position;
            var eyePosition = DevicesRef.Instance.CameraRigRef.CameraRig.centerEyeAnchor.position;
            transform.position = centerPosition + radius * (eyePosition - centerPosition).normalized;
            UpdateToggles();
        }
    }
}
