using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vsens.controls
{
    [RequireComponent(typeof(Toggle))]
    public class IMUTypeToggle : MonoBehaviour
    {
        public VsensPlatform VsensPlatform;
        public TextMeshProUGUI label;
        private Toggle _toggle;
        
        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
        }
        
        private void Start()
        {
            if (VsensPlatform != null)
            {
                _toggle.SetIsOnWithoutNotify(VsensPlatform.IsAccMode);
                if (label != null)
                {
                    label.text = VsensPlatform.IsAccMode ? "IMU Type: Acc" : "IMU Type: Gyro";
                }
            }
            _toggle.onValueChanged.AddListener(value =>
            {
                if (VsensPlatform == null) return;
                VsensPlatform.IsAccMode = value;
                if (label != null)
                {
                    label.text = value ? "IMU Type: Acc" : "IMU Type: Gyro";
                }
            });
        }
    }
}
