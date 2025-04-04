using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IMU.trajectory
{
    [ExecuteAlways]
	[RequireComponent(typeof(LineRenderer))]
    public class TrajectoryRenderer : MonoBehaviour
    {
        [Header("Control Points (Transforms)")]
        public List<Transform> controlPoints = new();

        [Header("Spline Settings")]
        [Range(1, 50)] public int pointsPerSegment = 10;

        [Header("Line Settings")]
        public float lineWidth = 0.01f;
        public Gradient colorGradient;

        private LineRenderer lr;

        private void Awake()
        {
            lr = GetComponent<LineRenderer>();
        }

        void OnEnable()
        {
            Init();
            UpdatePath();
        }
        
        void OnValidate()
        {
            Init();
            UpdatePath();
        }
        
        void Update()
        {
            if (!Application.isPlaying)
            {
                UpdatePath();
            }
        }

        void Init()
        {
            if (lr == null)
            {
                lr = GetComponent<LineRenderer>();
            }
            lr.widthMultiplier = lineWidth;
            lr.colorGradient = colorGradient;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
        }

        public void UpdatePath()
        {
            if (controlPoints == null || controlPoints.Count < 2)
            {
                lr.positionCount = 0;
                return;
            }

            List<Vector3> points = new List<Vector3>();

            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)].position;
                Vector3 p1 = controlPoints[i].position;
                Vector3 p2 = controlPoints[i + 1].position;
                Vector3 p3 = controlPoints[Mathf.Min(i + 2, controlPoints.Count - 1)].position;

                for (int j = 0; j < pointsPerSegment; j++)
                {
                    float t = j / (float)pointsPerSegment;
                    Vector3 pt = CatmullRom(p0, p1, p2, p3, t);
                    points.Add(pt);
                }
            }

            points.Add(controlPoints[controlPoints.Count - 1].position);

            lr.positionCount = points.Count;
            lr.colorGradient = colorGradient;
            lr.SetPositions(points.ToArray());
        }

        Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
            );
        }
    }


    [CustomEditor(typeof(TrajectoryRenderer))]
    public class PathRendererEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Update Path"))
            {
                ((TrajectoryRenderer)target).UpdatePath();
            }
        }
    }
}
