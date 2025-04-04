using System;
using System.Collections.Generic;
using Sensor;
using UnityEngine;
using XCharts.Runtime;

namespace IMU.data
{
    [RequireComponent(typeof(LineChart))]
    public class IMUChart : MonoBehaviour
    {
        public RectTransform left, right, indicator, trimLeft, trimRight, previewRange; 
        private LineChart chart;
        private List<SensorData> currentIMUData = new();
        private bool _isAccMode = true;
        private float _progress = 0.5f;
        public float PreviewRange { get; set; } = 0.2f;
        public float Progress
        {
            get => _progress;
            set
            {
                if (Mathf.Approximately(_progress, value)) return;
                _progress = value;
                var progress = Mathf.Clamp(Progress, 0, 1);
                var length = right.anchoredPosition.x - left.anchoredPosition.x;
                indicator.anchoredPosition = new Vector2(left.anchoredPosition.x + length * progress, indicator.anchoredPosition.y);
            }
        }
        
        public Action<float> jumpProgress = (progress) => { };
        
        public bool IsAccMode
        {
            get => _isAccMode;
            set
            {
                if (_isAccMode == value) return;
                _isAccMode = value;
                drawIMUData();
            }
        }
        

        private void Awake()
        {
            chart = GetComponent<LineChart>();
        }

        private void Update()
        {
            // update preview range
            // TODO scissor
            var length = right.anchoredPosition.x - left.anchoredPosition.x;
            previewRange.sizeDelta = new Vector2(length * PreviewRange, previewRange.sizeDelta.y);
        }

        public void updateIMUData(List<SensorData> imuData)
        {
            currentIMUData = imuData;
            drawIMUData();
        }

        public void drawIMUData()
        {
            chart.ClearData();
            currentIMUData.ForEach(item => {
                var timeFormat = item.time.ToString("F2");
                var data = (VirtualIMUSensor.IMUSensorData) item.data;
                chart.AddXAxisData(timeFormat);
                if (_isAccMode)
                {
                    chart.AddData(0, data.LocalAcceleration.x); // x
                    chart.AddData(1, data.LocalAcceleration.y); // y
                    chart.AddData(2, data.LocalAcceleration.z); // z
                }
                else
                {
                    chart.AddData(0, data.Orientation.x); // x
                    chart.AddData(1, data.Orientation.y); // y
                    chart.AddData(2, data.Orientation.z); // z
                }
            });
        }
    }
}
