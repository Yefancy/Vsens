using UnityEngine;

namespace smpl
{
    public class UIPanelPosition : MonoBehaviour
    {
        [SerializeField] private Transform axis;
        
        //runtime
        private float radius;

        private void Start()
        {
            var dir = axis.position - transform.position;
            dir.y = 0;
            radius = dir.magnitude;
        }

        void Update()
        {
            if (!(radius > 0)) return;
            var centerPosition = axis.position;
            var eyePosition = DevicesRef.Instance.CameraRigRef.CameraRig.centerEyeAnchor.position;
            var dir = eyePosition - centerPosition;
            dir.y = 0;
            var pos = centerPosition + radius * dir.normalized;
            pos.y = eyePosition.y;
            transform.position = pos;
        }
    }
}
