using System;
using Sensor.visualization;
using SimpleJSON;
using UnityEngine;

namespace Sensor
{
    public class VirtualLightSensor : VirtualSensor
    {
        public static readonly ISensorDefinition DEFINITION = ISensorDefinition.create("OPTICAL", "lux");
 
        private struct LightSensorData : ISensorData
        {
            public float lux;
            public string ToCsvLine()
            {
                return $"{lux}";
            }

            public JSONNode serialize()
            {
                var json = new JSONObject
                {
                    ["lux"] = lux
                };
                return json;
            }
        }
        
        [SerializeField] private float maxIntensity = 5;
        [SerializeField] private BarChart barChart;
        
        private LineChartController graphController;

        protected override void Start()
        {
            base.Start();
            barChart.ProgressTextFormatter = f => f.ToString("F2") + " lux";
            graphController = graphChart == null ? null : graphChart?.GetComponent<LineChartController>();
        }

        public override void UpdateWorking(float time, float deltaTime)
        {
            // calculate distance on the fly
            var intensity = CalculateLightIntensityAtPosition(transform.position);
            var lux = Mathf.Clamp(intensity / maxIntensity, 0, 1);
            barChart.progress = lux;
            var sensorData = new LightSensorData() {lux = lux};
            if (ShowGraph)
            {
                graphController?.UploadData(time, new[] {sensorData.lux});
            }
            AppendData(time, sensorData);
        }
        
        float CalculateLightIntensityAtPosition(Vector3 position)
        {
            float intensity = 0f;
            foreach (var light in LightManager.GetLights())
            {
                if (light.type == LightType.Directional)
                {
                    intensity += light.intensity;
                } else if (light.type == LightType.Point)
                {
                    var distance = Vector3.Distance(light.transform.position, position);
                    if (distance < light.range)
                    {
                        intensity += light.intensity / (distance * distance);
                    }
                } else if (light.type == LightType.Spot)
                {
                    var distance = Vector3.Distance(light.transform.position, position);
                    if (distance < light.range)
                    {
                        var angle = Vector3.Angle(light.transform.forward, position - light.transform.position);
                        if (angle < light.spotAngle)
                        {
                            intensity += light.intensity / (distance * distance);
                        }
                    }
                } else if (light.type == LightType.Rectangle)
                {
                    var distance = Vector3.Distance(light.transform.position, position);
                    if (distance < light.range)
                    {
                        var angle = Vector3.Angle(light.transform.forward, position - light.transform.position);
                        if (angle < light.spotAngle)
                        {
                            intensity += light.intensity / (distance * distance);
                        }
                    }
                }
            }
            return intensity;
        }

        public override ISensorDefinition SensorDefinition()
        {
            return DEFINITION;
        }
        
        public override SimpleJSON.JSONObject GetSensorDescription()
        {
            var description = base.GetSensorDescription();
            description["value"] = Math.Round(CalculateLightIntensityAtPosition(transform.position), 3);
            return description;
        }
    }
}


