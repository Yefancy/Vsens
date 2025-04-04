using System;
using TMPro;
using UnityEngine;

namespace Sensor.visualization
{
    public class BarChart : MonoBehaviour
    {
        [SerializeField] private GameObject barGameObject;
        [SerializeField] private GameObject textGameObject;
    
        private Func<float, string> progressTextFormatter = f => (f * 100).ToString("F0") + "%";
    
        public Func<float, string> ProgressTextFormatter
        {
            set
            {
                progressTextFormatter = value;
                UpdateText();
            }
        }
    
        public float progress
        {
            get => barGameObject.transform.localScale.y;
            set
            {
                var scale = barGameObject.transform.localScale;
                scale.y = value;
                barGameObject.transform.localScale = scale;
                barGameObject.transform.localPosition = new Vector3(0, value - 1, 0);
                UpdateText();
            }
        }

        private void UpdateText()
        {
            textGameObject.GetComponent<TextMeshPro>().text = progressTextFormatter(progress);
        }

        private void Start()
        {
            UpdateText();
        }
    
        private void Update()
        {
            if (Camera.main != null) textGameObject.transform.LookAt(
                textGameObject.transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
        }
    }
}
