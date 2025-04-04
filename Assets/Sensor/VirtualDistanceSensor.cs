using OVRSimpleJSON;
using Sensor.visualization;
using UnityEngine;

namespace Sensor
{
    public class VirtualDistanceSensor : VirtualSensor
    {
        public static readonly ISensorDefinition DEFINITION = ISensorDefinition.create("DISTANCE", "d");
 
        private struct DistanceSensorData : ISensorData
        {
            public float distance;
            public string ToCsvLine()
            {
                return $"{distance}";
            }

            public JSONNode serialize()
            {
                var json = new JSONObject
                {
                    ["distance"] = distance
                };
                return json;
            }
        }

        [SerializeField] private bool showBarPreview = true;
        [SerializeField] private float validDistance = 2;
        [SerializeField] private BarChart barChart;
        [SerializeField] private GameObject indicatorLine;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;
        
        private LineChartController graphController;

        public float Progress => barChart.progress;
        
        public float Distance => Progress * validDistance;
        
        public Vector3 StartPoint => startPoint.position;

        public Vector3 LookDirection
        {
            get
            {
                var direction = endPoint.position - startPoint.position;
                direction.Normalize();
                return direction;
            }
        }

        protected override void Start()
        {
            base.Start();
            barChart.ProgressTextFormatter = f => (f >= 1 ? ">= " + validDistance.ToString("F2") : (f * validDistance).ToString("F2")) + "m";
            onShowPreviewChanged += showPreview => {
                {
                    indicatorLine?.SetActive(showPreview);
                    preview?.SetActive(showBarPreview && showPreview);
                }
            };
            indicatorLine.SetActive(ShowPreview);
            preview?.SetActive(showBarPreview && ShowPreview);
            graphController = graphChart.GetComponent<LineChartController>();
        }

        public override void UpdateWorking(float time, float deltaTime)
        {
            // calculate distance on the fly
            var eyePosition = StartPoint;
            var lookingForward = LookDirection;
            var ray = new Ray(eyePosition, lookingForward);
            if (Physics.Raycast(ray, out var hit, validDistance))
            {
                if (isSelected)
                {
                    Debug.DrawRay(eyePosition, lookingForward * hit.distance, Color.green, 0.1f);
                }
                barChart.progress = hit.distance / validDistance;
            }
            else
            {
                barChart.progress = 1;
            }
            var sensorData = new DistanceSensorData() {distance = Distance};
            if (ShowGraph)
            {
                graphController?.UploadData(time, new[] {sensorData.distance});
            }
            AppendData(time, sensorData);
            var parentScale = indicatorLine.transform.parent.lossyScale;
            indicatorLine.transform.localScale = new Vector3(1F / parentScale.x, 1F / parentScale.z, sensorData.distance / parentScale.z);
        }

        public override ISensorDefinition SensorDefinition()
        {
            return DEFINITION;
        }
    }
}