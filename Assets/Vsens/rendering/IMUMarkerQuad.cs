using System;
using Sensor;
using UnityEngine;

namespace Vsens
{
    public class IMUMarkerQuad : MonoBehaviour
    {
        public VirtualIMUSensor imuSensor;
        public Camera targetCamera;
        public GameObject Quad;

        private void Start()
        {
            LateUpdate();
        }

        void LateUpdate()
        {
            if (imuSensor == null)
            {
                Quad.SetActive(false);
                return;
            }
            else
            {
                Quad.SetActive(true);
            }
            
            transform.position = imuSensor.transform.position;
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null) return;
            transform.LookAt(targetCamera.transform);
            transform.forward = -transform.forward;
        }
    }
}
