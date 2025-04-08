using System;
using System.Collections.Generic;
using Sensor;
using UnityEngine;
using UnityEngine.EventSystems;
using XCharts.Runtime;

namespace Vsens.data
{
    [RequireComponent(typeof(LineChart))]
    public class IMUChart : MonoBehaviour
    {
        public RectTransform left, right, indicator, trimLeft, trimRight; 
        public RectTransform leftPreview, indicatorPreviewLeft, indicatorPreviewRight, rightPreview; 
        private LineChart chart;
        private List<SensorData> currentIMUData = new();
        private bool _isAccMode = true;
        private float _progress = 0.5f;
        public float PreviewRange = 0.2f;
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
        
        public Action<float> JumpProgress { get; set; }
        
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
            chart.onDrag = OnChartDrag;
            chart.onPointerClick = OnChartClick;
        }
        
        private void OnChartClick(PointerEventData eventData, BaseGraph graph)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(graph.canvas.transform as RectTransform,
                    eventData.position,
                    graph.canvas.worldCamera, out var position)) return;
            var length = right.anchoredPosition.x - left.anchoredPosition.x;
            var leftPos = left.anchoredPosition;
            var progress = Mathf.Clamp((position.x - leftPos.x) * 1f / length, 0, 1);
            JumpProgress?.Invoke(progress);
        }
        
        private void OnChartDrag(PointerEventData eventData, BaseGraph graph)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(graph.canvas.transform as RectTransform,
                    eventData.position,
                    graph.canvas.worldCamera, out var position)) return;
            var length = right.anchoredPosition.x - left.anchoredPosition.x;
            var leftPos = left.anchoredPosition;
            var progress = Mathf.Clamp((position.x - leftPos.x) * 1f / length, 0, 1);
            JumpProgress?.Invoke(progress);
        }

        private void Update()
        {
            // update preview range
            // TODO scissor
            var length = right.anchoredPosition.x - left.anchoredPosition.x;
            var halfLength = length * PreviewRange / 2;
            var indicatorPos = indicator.anchoredPosition.x;
            var leftLeft = indicatorPos - left.anchoredPosition.x;
            var rightLeft = right.anchoredPosition.x - indicatorPos;
            if (halfLength <= leftLeft)
            {
                indicatorPreviewLeft.sizeDelta = new Vector2(halfLength, indicatorPreviewLeft.sizeDelta.y);
                rightPreview.sizeDelta = new Vector2(0, rightPreview.sizeDelta.y);
            }
            else
            {
                indicatorPreviewLeft.sizeDelta = new Vector2(leftLeft, indicatorPreviewLeft.sizeDelta.y);
                rightPreview.sizeDelta = new Vector2(halfLength - leftLeft, rightPreview.sizeDelta.y);
            }
            if (halfLength <= rightLeft)
            {
                indicatorPreviewRight.sizeDelta = new Vector2(halfLength, indicatorPreviewRight.sizeDelta.y);
                leftPreview.sizeDelta = new Vector2(0, leftPreview.sizeDelta.y);
            }
            else
            {
                indicatorPreviewRight.sizeDelta = new Vector2(rightLeft, indicatorPreviewRight.sizeDelta.y);
                leftPreview.sizeDelta = new Vector2(halfLength - rightLeft, leftPreview.sizeDelta.y);
            }
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
