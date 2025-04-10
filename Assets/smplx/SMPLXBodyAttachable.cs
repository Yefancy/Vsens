using Sensor;
using UnityEngine;

namespace smplx
{
    public class SMPLXBodyAttachable : SensorAttachable
    {
        public SMPLX smplx;
        
        private void Awake()
        {
            if (smplx == null)
            {
                smplx = GetComponentInParent<SMPLX>();
            }
        }
        
        public static float PointToSegmentDistance(Vector3 point, Vector3 a, Vector3 b, out Vector3 closestPoint)
        {
            Vector3 ab = b - a;
            Vector3 ap = point - a;
            float abLengthSquared = ab.sqrMagnitude;

            // 投影系数 t ∈ [0, 1]
            float t = Vector3.Dot(ap, ab) / abLengthSquared;
            t = Mathf.Clamp01(t);

            closestPoint = a + t * ab;
            return Vector3.Distance(point, closestPoint);
        }
        
        private Transform FindNearestBone(Vector3 position)
        {
            bool found = false;
            float minDistance = float.MaxValue;
            SMPLX.Bone nearestBone = new SMPLX.Bone();

            foreach (var bone in smplx.Bones)
            {
                float distance = PointToSegmentDistance(position, bone.From.position, bone.To.position, out _);
                // Debug.Log(bone.ToString() + " distance: " + distance);
                if (distance < minDistance && distance < 0.3f)
                {
                    minDistance = distance;
                    nearestBone = bone;
                    found = true;
                }
            }

            return found ? nearestBone.Parent : null;
        }
        
        protected override bool OnAttachInternal(VirtualSensor sensor)
        {
            var nearestBone = FindNearestBone(sensor.transform.position);
            if (nearestBone != null)
            {
                Debug.Log("Find the nearest bone: " + nearestBone.name);
                sensor.transform.SetParent(nearestBone);
                return true;
            }

            return false;
        }
    }
}
