using System;
using System.Collections.Generic;
using System.Linq;
using OVRSimpleJSON;
using Sensor.visualization;
using UnityEngine;

namespace Sensor
{
    public class VirtualIMUSensor : VirtualSensor
    {

        public static readonly ISensorDefinition DEFINITION = ISensorDefinition.create("IMU", "ex,ey,ez,ax,ay,az,lx,ly,lz,x,y,z");
    
        public struct IMUSensorData : ISensorData
        {
            public Vector3 Orientation;
            public Vector3 Acceleration;
            public Vector3 LocalAcceleration;
            public Vector3 Location;
            public string ToCsvLine()
            {
                return $"{Orientation.x},{Orientation.y},{Orientation.z},{Acceleration.x},{Acceleration.y},{Acceleration.z},{LocalAcceleration.x},{LocalAcceleration.y},{LocalAcceleration.z},{Location.x},{Location.y},{Location.z}";
            }

            public JSONNode serialize()
            {
                var json = new JSONObject
                {
                    ["orientation"] = Utils.SerializeVector3(Orientation),
                    ["acceleration"] = Utils.SerializeVector3(Acceleration),
                    ["localAcceleration"] = Utils.SerializeVector3(LocalAcceleration),
                    ["location"] = Utils.SerializeVector3(Location)
                };
                return json;
            }
        }
    
        
        [SerializeField] private AxisAnchor AnchorVisual;
        
        
        // run-time
        private float _duration;
        private Vector3 _lastPosition = Vector3.zero;
        private Vector3 _lastRotation = Vector3.zero;
        private Vector3 _lastSpeed = Vector3.zero;
        private Vector3 _lastAcceleration = Vector3.zero;
        private readonly List<Tuple<float, Vector3>> _positionCache = new();
        private LineChartController graphController;
        
        protected override void Start()
        {
            base.Start();
            _lastPosition = transform.position;
            _lastRotation = transform.rotation.eulerAngles;
            graphController = graphChart.GetComponent<LineChartController>();
        }

        /// <summary>
        /// it will be called if and only if the sensor is working.
        /// </summary>
        public override void UpdateWorking(float time, float deltaTime)
        {
            if (sensorDataCenter == null) return;
            _duration += deltaTime;
            var interval = sensorDataCenter.SamplingInterval;

            if (isSelected && !sensorDataCenter.IsSimulating)
            {
                Debug.DrawRay(transform.position, transform.forward * -2f, Color.red);
                Debug.DrawRay(transform.position, transform.up * 2f, Color.green);
                Debug.DrawRay(transform.position, transform.right * 2f, Color.blue);
            }

            _positionCache.Add(new Tuple<float, Vector3>(time, transform.position));
            while (_positionCache.Count > 1 && _positionCache[0].Item1 < time - sensorDataCenter.smoothWindowSize)
            {
                _positionCache.RemoveAt(0);
            }
        
            if (_duration >= interval) {
                var smoothPosition = _positionCache.Select(tuple => tuple.Item2).Aggregate(Vector3.zero, (current, p) => current + p) / _positionCache.Count;
                var avgSpeed = (smoothPosition - _lastPosition) / _duration;
                var avgAcceleration = ((avgSpeed - _lastSpeed) / _duration) + new Vector3(0, -9.8f, 0);
                var d = interval;
                var lastTime = time - deltaTime;
                var lerp = 1f;
            
                while (_duration - d >= 0)
                {
                    lerp = d / _duration;
                    var t = Mathf.Lerp(lastTime, time, lerp);
                    var angular = SetSmallComponentsToZero(Vector3.Lerp(_lastRotation, transform.rotation.eulerAngles, lerp));
                    var acceleration = SetSmallComponentsToZero(Vector3.Lerp(_lastAcceleration, avgAcceleration, lerp));
                    // if (angular.x) 
                    d += interval;
                
                    // TODO: coordinate transform
                    var localAcceleration = ToLocal(acceleration);
                    var sensorData = new IMUSensorData
                    {
                        Orientation = angular,
                        Acceleration = acceleration,
                        LocalAcceleration = localAcceleration,
                        Location = smoothPosition
                    };
                    if (Mathf.Abs(sensorData.LocalAcceleration.magnitude) < 80)
                    {
                        AnchorVisual.anchor = sensorData.LocalAcceleration;
                        AppendData(t, sensorData);
                        if (ShowGraph)
                        {
                            graphController?.UploadData(t, new[]
                            {
                                sensorData.LocalAcceleration.x,
                                sensorData.LocalAcceleration.y,
                                sensorData.LocalAcceleration.z,
                            });
                        }
                    }
                }
            
                // coordinate transform
                // avgSpeed = Quaternion.Euler(0, -FPSDisplayer.DEGREE, 0) * avgSpeed;
                // avgSpeed = new Vector3(avgSpeed.x, avgSpeed.z, avgSpeed.y);
                //
                // var position = Quaternion.Euler(0, -FPSDisplayer.DEGREE, 0) * smoothPosition;
                // position = new Vector3(position.x, position.z, position.y);
            
                _duration *= (1 - lerp);
                _lastPosition = Vector3.Lerp(_lastPosition, smoothPosition, lerp);
                _lastRotation = Vector3.Lerp(_lastRotation, transform.rotation.eulerAngles, lerp);
                _lastSpeed = Vector3.Lerp(_lastSpeed, avgSpeed, lerp);
                _lastAcceleration = Vector3.Lerp(_lastAcceleration, avgAcceleration, lerp);
            }
        }
    
        public static Vector3 SetSmallComponentsToZero(Vector3 vector, float threshold = 0.0001f)
        {
            return new Vector3(
                Mathf.Abs(vector.x) < threshold ? 0 : vector.x,
                Mathf.Abs(vector.y) < threshold ? 0 : vector.y,
                Mathf.Abs(vector.z) < threshold ? 0 : vector.z
            );
        }
    
        private Vector3 ToLocal(Vector3 v) {
            var vx_prime = Vector3.Dot(v, transform.forward);
            var vy_prime = Vector3.Dot(v, transform.up);
            var vz_prime = Vector3.Dot(v, transform.right);
            return new Vector3(vy_prime, vz_prime, vx_prime);
        }

        public override ISensorDefinition SensorDefinition()
        {
            return DEFINITION;
        }

        public override void ClearData()
        {
            base.ClearData();
            _duration = 0;
        }
    
        public override void ClearSmoothCache()
        {
            _positionCache.Clear();
            _lastPosition = transform.position;
            _lastRotation = transform.rotation.eulerAngles;
            _lastSpeed = Vector3.zero;
            _lastAcceleration = Vector3.zero;
        }
    }
}
