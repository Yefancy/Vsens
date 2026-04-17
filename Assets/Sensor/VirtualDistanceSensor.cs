using System;
using Sensor.visualization;
using SimpleJSON;
using UnityEngine;
using JSONArray = SimpleJSON.JSONArray;
using JSONNumber = SimpleJSON.JSONNumber;

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
        [SerializeField] public float validDistance = 2;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField] private bool ignoreOwnColliders = true;
        [SerializeField] private float rayOriginOffset = 0.02f;
        [SerializeField] private BarChart barChart;
        [SerializeField] private GameObject indicatorLine;
        [SerializeField] private Transform startPoint;
        [SerializeField] private Transform endPoint;
        
        private LineChartController graphController;
        private float lastDistance;
        private string lastHitObjectName = "";

        public float Progress => validDistance > 0f ? Mathf.Clamp01(Distance / validDistance) : 0f;
        
        public float Distance => lastDistance;
        
        public string LastHitObjectName => lastHitObjectName;
        
        public Vector3 StartPoint => startPoint != null ? startPoint.position : transform.position;

        public Vector3 LookDirection
        {
            get
            {
                if (startPoint != null && endPoint != null)
                {
                    var direction = endPoint.position - startPoint.position;
                    if (direction.sqrMagnitude > 0.000001f)
                    {
                        return direction.normalized;
                    }
                }

                return transform.forward;
            }
        }

        protected override void Start()
        {
            base.Start();
            lastDistance = validDistance;
            if (barChart != null)
            {
                barChart.ProgressTextFormatter = f => (f >= 1 ? ">= " + validDistance.ToString("F2") : (f * validDistance).ToString("F2")) + "m";
            }
            onShowPreviewChanged += showPreview => {
                {
                    indicatorLine?.SetActive(showPreview);
                    preview?.SetActive(showBarPreview && showPreview);
                }
            };
            indicatorLine?.SetActive(ShowPreview);
            preview?.SetActive(showBarPreview && ShowPreview);
            graphController = graphChart != null ? graphChart.GetComponent<LineChartController>() : null;
        }

        public override void UpdateWorking(float time, float deltaTime)
        {
            // calculate distance on the fly
            var eyePosition = StartPoint;
            var lookingForward = LookDirection;
            if (TryRaycastDistance(eyePosition, lookingForward, out var hit))
            {
                if (isSelected)
                {
                    Debug.DrawRay(eyePosition, lookingForward * hit.distance, Color.green, 0.1f);
                }
                lastDistance = Mathf.Clamp(hit.distance, 0f, validDistance);
                lastHitObjectName = hit.collider != null ? hit.collider.gameObject.name : "";
            }
            else
            {
                lastDistance = validDistance;
                lastHitObjectName = "";
            }
            
            if (barChart != null)
            {
                barChart.progress = Progress;
            }

            var sensorData = new DistanceSensorData() {distance = Distance};
            if (ShowGraph)
            {
                graphController?.UploadData(time, new[] {sensorData.distance});
            }
            AppendData(time, sensorData);
            if (indicatorLine != null && indicatorLine.transform.parent != null)
            {
                var parentScale = indicatorLine.transform.parent.lossyScale;
                indicatorLine.transform.localScale = new Vector3(1F / parentScale.x, 1F / parentScale.z, sensorData.distance / parentScale.z);
            }
        }

        private bool TryRaycastDistance(Vector3 origin, Vector3 direction, out RaycastHit closestHit)
        {
            closestHit = default;
            if (validDistance <= 0f || direction.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            direction.Normalize();
            float offset = Mathf.Clamp(rayOriginOffset, 0f, validDistance);
            Vector3 castOrigin = origin + direction * offset;
            float castDistance = Mathf.Max(0f, validDistance - offset);
            var hits = Physics.RaycastAll(castOrigin, direction, castDistance, hitMask, triggerInteraction);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                if (ignoreOwnColliders && hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                closestHit = hit;
                closestHit.distance += offset;
                return true;
            }

            return false;
        }

        public override ISensorDefinition SensorDefinition()
        {
            return DEFINITION;
        }
        
        public override SimpleJSON.JSONObject GetSensorDescription()
        {
            var description = base.GetSensorDescription();
            description["validDistance"] = validDistance;
            description["value"] = Math.Round(Distance, 3);
            if (!string.IsNullOrEmpty(lastHitObjectName))
            {
                description["hitObject"] = lastHitObjectName;
            }
            var lookDirectionArray = new JSONArray();
            lookDirectionArray.Add(new JSONNumber(Math.Round(LookDirection.x, 3)));
            lookDirectionArray.Add(new JSONNumber(Math.Round(LookDirection.y, 3)));
            lookDirectionArray.Add(new JSONNumber(Math.Round(LookDirection.z, 3)));
            description["lookRay"] = lookDirectionArray;
            var point = new JSONArray();
            point.Add(new JSONNumber(Math.Round(StartPoint.x, 3)));
            point.Add(new JSONNumber(Math.Round(StartPoint.y, 3)));
            point.Add(new JSONNumber(Math.Round(StartPoint.z, 3)));
            description["startPoint"] = point;
            return description;
        }
    }
}
