using TMPro;
using UnityEngine;

namespace Sensor.visualization
{
    public class AxisAnchor : MonoBehaviour
    {
        [SerializeField] private GameObject xCube;
        [SerializeField] private GameObject yCube;
        [SerializeField] private GameObject zCube;

        [SerializeField] private TextMeshPro xText;
        [SerializeField] private TextMeshPro yText;
        [SerializeField] private TextMeshPro zText;
        
        public Vector3 anchor
        {
            set
            {
                var normalized = value.normalized;
                xCube.transform.localPosition = new Vector3(0, normalized.x * 0.2f, 0);
                yCube.transform.localPosition = new Vector3(normalized.y * 0.2f, 0, 0);
                zCube.transform.localPosition = new Vector3(0, 0, normalized.z * 0.2f);
                
                xText.text = normalized.x.ToString("F2");
                yText.text = normalized.y.ToString("F2");
                zText.text = normalized.z.ToString("F2");
            }
        }
        
        void Update()
        {
            if (Camera.main != null)
            {
                
                var worldUp = Camera.main.transform.rotation * Vector3.up;
                var cameraForward = Camera.main.transform.rotation * Vector3.forward;
                xText.transform.LookAt(xText.transform.position + cameraForward, worldUp);
                yText.transform.LookAt(yText.transform.position + cameraForward, worldUp);
                zText.transform.LookAt(zText.transform.position + cameraForward, worldUp);
            }
        }
    }
}
