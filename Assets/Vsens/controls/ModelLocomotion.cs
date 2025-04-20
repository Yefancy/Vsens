using Meta.XR;
using UnityEngine;

namespace Vsens.controls
{
    public class ModelLocomotion : MonoBehaviour
    {
        public GameObject target;
        
        // Update is called once per frame
        void Update()
        {
            if (target == null) return;
            // move
            var force = OVRInput.Get(OVRInput.RawAxis2D.RThumbstick);
            if (force.magnitude > 0.1f)
            {
                // move based on the camera forward direction
                var cameraRig = DevicesRef.Instance.CameraRigRef.CameraRig;
                var forward = cameraRig.centerEyeAnchor.transform.forward;
                forward.y = 0;
                var movement = new Vector3(force.x, 0, force.y) * 0.05f;
                // rotate the movement vector based on the camera forward direction
                var rotation = Quaternion.LookRotation(forward);
                movement = rotation * movement;
                target.transform.position += movement;
            }
            // rotation + y offset
            force = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick);
            if (force.magnitude > 0.1f)
            {
                if (Mathf.Abs(force.y) > Mathf.Abs(force.x))
                {
                    // y offset
                    var movement = new Vector3(0, force.y, 0) * 0.05f;
                    target.transform.position += movement;
                }
                else
                {
                    // rotation
                    var rotation = Quaternion.Euler(0, force.x * 2, 0);
                    target.transform.rotation *= rotation;
                }
            }
        }
    }
}
