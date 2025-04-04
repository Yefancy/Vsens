using UnityEngine;

namespace Sensor
{
    public class BoneMeshAttachment
    {
        public GameObject sensor;
        public int triangleIndex;
        public int boneIndex;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public MeshCollider collider;
        public Vector3 positionOffset = Vector3.zero;
        public float rotationZ = 0;
        
        public BoneMeshAttachment(GameObject sensor, int triangleIndex, int boneIndex, SkinnedMeshRenderer skinnedMeshRenderer, MeshCollider collider)
        {
            this.sensor = sensor;
            this.triangleIndex = triangleIndex;
            this.boneIndex = boneIndex;
            this.skinnedMeshRenderer = skinnedMeshRenderer;
            this.collider = collider;
        }

        public void UpdateTransform()
        {
            var vertices = collider.sharedMesh.vertices;
            var triangles = collider.sharedMesh.triangles;
            var p0 = vertices[triangles[triangleIndex * 3 + 0]];
            var p1 = vertices[triangles[triangleIndex * 3 + 1]];
            var p2 = vertices[triangles[triangleIndex * 3 + 2]];
            
            p0 = collider.transform.TransformPoint(p0);
            p1 = collider.transform.TransformPoint(p1);
            p2 = collider.transform.TransformPoint(p2);
            
            sensor.transform.position = p0 + positionOffset;
            var normal = Utils.CalculateNormal(p0, p1, p2);
            var rotation = Quaternion.LookRotation(normal);
            sensor.transform.rotation = rotation;
            Debug.DrawLine(sensor.transform.position, normal * 100, Color.magenta);
        }
    }
}