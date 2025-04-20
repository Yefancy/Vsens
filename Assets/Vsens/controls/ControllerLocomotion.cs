using Meta.XR;
using UnityEngine;

namespace Vsens.controls
{
    public class ControllerLocomotion : MonoBehaviour
    {
        public EnvironmentRaycastManager raycastManager;
        public GameObject preview;
        public GameObject target;
        
        
        
        // runtime
        private bool _isMoving;
        
        void Start()
        {
            if (raycastManager == null)
            {
                raycastManager = FindFirstObjectByType<EnvironmentRaycastManager>();
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (raycastManager == null || !EnvironmentRaycastManager.IsSupported) return;
            if (OVRInput.Get(OVRInput.RawButton.B))
            {
                if (!_isMoving)
                {
                    _isMoving = true;
                    raycastManager.enabled = true;
                    preview.SetActive(true);
                }
                return;
            } 
            
            if (OVRInput.Get(OVRInput.RawButton.A))
            {
                if (_isMoving)
                {
                    _isMoving = false;
                    raycastManager.enabled = false;
                    preview.SetActive(false);
                    target.transform.position = preview.transform.position;
                    target.transform.rotation = preview.transform.rotation;
                    preview.SetActive(false);
                    preview.transform.position = Vector3.zero;
                    preview.transform.rotation = Quaternion.identity;
                }
                return;
            }

            if (_isMoving)
            {
                // Check if a surface below the object is hit
                if (raycastManager.Raycast(new Ray(DevicesRef.Instance.RightRayInteractor.Origin, DevicesRef.Instance.RightRayInteractor.Forward), out var hitInfo))
                {
                    preview.transform.position = hitInfo.point;
                    // rotation face the camera
                    var cameraRig = DevicesRef.Instance.CameraRigRef.CameraRig;
                    var forward = cameraRig.centerEyeAnchor.transform.forward;
                    forward.y = 0;
                    preview.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
                }
            }
        }
    }
}
