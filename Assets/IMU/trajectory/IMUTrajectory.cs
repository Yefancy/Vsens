using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Serialization;

namespace IMU.trajectory
{
    [RequireComponent(typeof(TrajectoryRenderer))]
    public class IMUTrajectory : MonoBehaviour
    {
        public enum MappingFunctionType 
        {
            Sigmoid,
            Tanh,
            Logistic
        }
        private TrajectoryRenderer _trajectoryRenderer;
        public Vector3 offset = new Vector3(0, 0.3f, 0);
        public int controllerPointCount = 17;
        public float mappingDistance = 1 / 6f;
        public float steepness = 4f;
        public MappingFunctionType  mappingType = MappingFunctionType.Sigmoid;

        public float Progress = 0f;
        public float PreviewRange = 0.2f;
        public Color minColor = Color.cyan;
        public Color maxColor = Color.red;

        private List<Vector3> _sensorData;
        private TransformerUtils.FloatRange _valueRange;    
        private TransformerUtils.FloatRange _magRange;
        private Vector3[] _mappedOffsets;

        private void Awake()
        {
            _trajectoryRenderer = GetComponent<TrajectoryRenderer>();
        }

        private void OnEnable()
        {
            GenerateControllerPoints();
        }

        public void GenerateControllerPoints()
        {
            foreach (var controlPoint in _trajectoryRenderer.controlPoints)
            {
                Destroy(controlPoint.gameObject);
            }
            _trajectoryRenderer.controlPoints.Clear();
            if (controllerPointCount >= 2)
            {
                var distance = 1f / (controllerPointCount - 1);
                for(int i = 0; i < controllerPointCount; i++)
                {
                    var controlPoint = new GameObject($"ControlPoint_{i}").transform;
                    controlPoint.SetParent(transform);
                    controlPoint.localPosition = new Vector3(0.5f - i * distance, 0, 0) + offset;
                    _trajectoryRenderer.controlPoints.Add(controlPoint);
                }
            }
            _trajectoryRenderer.UpdatePath();
        }

        public void UpdateIMUData(List<Vector3> sensorData, float minValue, float maxValue, float minMag, float maxMag)
        {
            if (_trajectoryRenderer.controlPoints.Count < 2 || sensorData.Count == 0) return;

            _sensorData = sensorData;
            _valueRange = new TransformerUtils.FloatRange { Min = minValue, Max = maxValue };
            _magRange = new TransformerUtils.FloatRange { Min = minMag, Max = maxMag };
            CalculateControllerPoints();
            DrawTrajectory();
        }

        public void CalculateControllerPoints()
        {
            _mappedOffsets = new Vector3[_sensorData.Count];
            for (int i = 0; i < _sensorData.Count; i++)
            {
                _mappedOffsets[i] = Map(_sensorData[i], _valueRange.Min, _valueRange.Max, mappingDistance / 2f, mappingType, steepness);
            }
        }

        public void DrawTrajectory()
        {
            if (_sensorData == null || _sensorData.Count == 0 || _mappedOffsets == null) return;

            var distance = 1f / (controllerPointCount - 1);
            float startT = Progress - PreviewRange / 2f;
            float endT = Progress + PreviewRange / 2f;

            for (int i = 0; i < _trajectoryRenderer.controlPoints.Count; i++)
            {
                float localT = i / (float)(_trajectoryRenderer.controlPoints.Count - 1); // ✅ 加上这个反向
                float t = Mathf.Repeat(Mathf.Lerp(startT, endT, localT), 1f);
                float indexF = t * (_sensorData.Count - 1);
                int index0 = Mathf.FloorToInt(indexF);
                int index1 = (index0 + 1) % _sensorData.Count;
                float lerpT = indexF - index0;

                Vector3 mapped = Vector3.Lerp(_mappedOffsets[index0], _mappedOffsets[index1], lerpT);
                var cp = _trajectoryRenderer.controlPoints[i];
                cp.localPosition = new Vector3(0.5f - i * distance, 0, 0) + offset + mapped;
            }

            Gradient gradient = _trajectoryRenderer.colorGradient;
            int keyCount = Mathf.Min(7, _trajectoryRenderer.controlPoints.Count);
            GradientColorKey[] colorKeys = new GradientColorKey[keyCount];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[keyCount];

            for (int k = 0; k < keyCount; k++)
            {
                int i = Mathf.RoundToInt((_trajectoryRenderer.controlPoints.Count - 1) * k / (float)(keyCount - 1));
                float localT = i / (float)(_trajectoryRenderer.controlPoints.Count - 1);
                float t = Mathf.Repeat(Mathf.Lerp(startT, endT, localT), 1f);
                float indexF = t * (_sensorData.Count - 1);
                int dataIndex = Mathf.Clamp(Mathf.RoundToInt(indexF), 0, _sensorData.Count - 1);
                Vector3 raw = _sensorData[dataIndex];
                float mag = raw.magnitude;

                float norm = Mathf.Clamp((mag - _magRange.Min) / (_magRange.Max - _magRange.Min), -1f, 1f);
                float centered = norm * 2f - 1f;
                float sig = (2f / (1f + Mathf.Exp(-steepness * centered)) - 1f);
                float weight = Mathf.Clamp01((sig + 1f) / 2f);
                Color c = Color.Lerp(minColor, maxColor, weight);

                colorKeys[k] = new GradientColorKey(c, localT);
                alphaKeys[k] = new GradientAlphaKey(1f, localT);
            }

            alphaKeys[0].alpha = 0f;
            alphaKeys[^1].alpha = 0f;
            gradient.SetKeys(colorKeys, alphaKeys);
            _trajectoryRenderer.UpdatePath();
        }

        public static Vector3 Map(Vector3 x, float minValue, float maxValue, float scale, MappingFunctionType type, float steepness = 1f)
        {
            switch (type)
            {
                case MappingFunctionType.Tanh:
                    return TanhMap(x, minValue, maxValue, scale);
                case MappingFunctionType.Logistic:
                    return LogisticMap(x, minValue, maxValue, scale, steepness);
                default:
                    return SigmoidMap(x, minValue, maxValue, scale, steepness);
            }
        }

        private static Vector3 SigmoidMap(Vector3 v, float minValue, float maxValue, float scale, float steepness)
        {
            float Sigmoid(float x)
            {
                float norm = (x - minValue) / (maxValue - minValue);
                float centered = norm * 2f - 1f; // [-1, 1]
                return (2f / (1f + Mathf.Exp(-steepness * centered)) - 1f) * scale;
            }
            return new Vector3(Sigmoid(v.x), Sigmoid(v.y), Sigmoid(v.z));
        }

        private static Vector3 TanhMap(Vector3 v, float minValue, float maxValue, float scale)
        {
            float Tanh(float x)
            {
                float norm = (x - minValue) / (maxValue - minValue);
                float centered = norm * 2f - 1f;
                float e1 = Mathf.Exp(centered);
                float e2 = Mathf.Exp(-centered);
                return (e1 - e2) / (e1 + e2) * scale;
            }
            return new Vector3(Tanh(v.x), Tanh(v.y), Tanh(v.z));
        }

        private static Vector3 LogisticMap(Vector3 v, float minValue, float maxValue, float scale, float steepness)
        {
            float Logistic(float x)
            {
                float norm = (x - minValue) / (maxValue - minValue);
                float centered = norm * 2f - 1f;
                return (2f / (1f + Mathf.Exp(-steepness * centered)) - 1f) * scale;
            }
            return new Vector3(Logistic(v.x), Logistic(v.y), Logistic(v.z));
        }
    }

    [UnityEditor.CustomEditor(typeof(IMUTrajectory))]
    public class IMUTrajectoryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var trajectory = (IMUTrajectory)target;
            if (GUILayout.Button("Generate Controller Points"))
            {
                trajectory.GenerateControllerPoints();
                trajectory.CalculateControllerPoints();
                trajectory.DrawTrajectory();
            }
            if (GUILayout.Button("Update Trajectory"))
            {
                trajectory.CalculateControllerPoints();
                trajectory.DrawTrajectory();
            }
        }
    }
}
