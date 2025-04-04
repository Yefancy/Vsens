using UnityEngine;

namespace smpl
{
    public class SMPLPreviewModel : IPreviewModel
    {
        [SerializeField] private Collider _collider;
        [SerializeField] private Material _material;
        [SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;
        
        public override void SetAsPreviewView()
        {
            _collider.enabled = false;
            _skinnedMeshRenderer.material = _material;
        }
        
        public override bool CanBePlacedAt(Ray ray, RaycastHit hit)
        {
            return true;
        }
        
        public override Pose GetPlacementPose(Ray ray, RaycastHit hit)
        {
            var dir = ray.direction;
            dir.y = 0;
            return new Pose(hit.point,  Quaternion.FromToRotation(Vector3.back, dir));
        }
        
        public void onHeightChange(float height)
        {
            if (_skinnedMeshRenderer != null)
            {
                if (height > 0)
                {
                    _skinnedMeshRenderer.SetBlendShapeWeight(1, 100 * height);
                    _skinnedMeshRenderer.SetBlendShapeWeight(0, 0);
                }
                else
                {
                    _skinnedMeshRenderer.SetBlendShapeWeight(1, 0);
                    _skinnedMeshRenderer.SetBlendShapeWeight(0, -100 * height);
                }
            }
        }
        
        public void onWeightChange(float weight)
        {
            if (_skinnedMeshRenderer != null)
            {
                if (weight > 0)
                {
                    _skinnedMeshRenderer.SetBlendShapeWeight(3, 100 * weight);
                    _skinnedMeshRenderer.SetBlendShapeWeight(2, 0);
                }
                else
                {
                    _skinnedMeshRenderer.SetBlendShapeWeight(3, 0);
                    _skinnedMeshRenderer.SetBlendShapeWeight(2, -100 * weight);
                }
            }
        }
    }
}
