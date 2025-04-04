using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sensor
{
    public class SensorStateController : MonoBehaviour
    {
        public bool IsActive;
        public bool IsSelected;
        public bool IsPreviewing;
        public bool IsGraphing;
        
        private bool lastIsActive;
        private bool lastIsSelected;
        private bool lastIsPreviewing;
        private bool lastIsGraphing;
        
        //runtime
        private readonly List<VirtualSensor> sensors = new();
        
        private void UpdateState(bool force = false)
        {
            if (force || IsActive != lastIsActive)
            {
                foreach (var sensor in sensors)
                {
                    sensor.IsActive = IsActive;
                }
                lastIsActive = IsActive;
            }
            if (force || IsSelected != lastIsSelected)
            {
                foreach (var sensor in sensors)
                {
                    sensor.isSelected = IsSelected;
                }
                lastIsSelected = IsSelected;
            }
            if (force || IsPreviewing != lastIsPreviewing)
            {
                foreach (var sensor in sensors)
                {
                    sensor.ShowPreview = IsPreviewing;
                }
                lastIsPreviewing = IsPreviewing;
            }
            if (force || IsGraphing != lastIsGraphing)
            {
                foreach (var sensor in sensors)
                {
                    sensor.ShowGraph = IsGraphing;
                }
                lastIsGraphing = IsGraphing;
            }
        }
        
        // Start is called before the first frame update
        void Start()
        {
            sensors.AddRange(GetComponentsInChildren<VirtualSensor>());
            UpdateState(true);
        }

        void Update()
        {
            UpdateState();
        }
    }
}
