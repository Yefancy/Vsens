using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Animations;

namespace Vsens.controls
{
    [RequireComponent(typeof(Toggle))]
    public class RotationToggle : MonoBehaviour
    {
        public VsensPlatform vsensPlatform;
        public Slider rotationSlider;
        public Axis Axis = Axis.None;
        
        private Toggle _toggle;
        private Quaternion _initialRotation;
        private Vector3 _worldAxis;
        private bool _isDragging;

        void Awake()
        {
            _toggle = GetComponent<Toggle>();
            _toggle.onValueChanged.AddListener(SetActive);
            
            rotationSlider.minValue = -180;  // 允许负值方便双向旋转
            rotationSlider.maxValue = 180;
            rotationSlider.wholeNumbers = true;
            
            var dragEvents = rotationSlider.GetComponent<SliderDragEvents>();
            dragEvents.onBeginDrag.AddListener(OnBeginDrag);
            dragEvents.onEndDrag.AddListener(OnEndDrag);
        }

        void Update()
        {
            if (!_toggle.isOn || vsensPlatform.selectedSensor == null) return;
            if (vsensPlatform.IsPlaying())
            {
                _toggle.isOn = false;
            }
        }

        private void UpdateSliderPosition()
        {
            // 重置Slider位置到中心
            rotationSlider.SetValueWithoutNotify(0f);
            if (vsensPlatform.selectedSensor != null)
            {
                _initialRotation = vsensPlatform.selectedSensor.transform.rotation;
                _worldAxis = GetLocalAxis();
            }
        }

        public void SetActive(bool active)
        {
            if (active)
            {
                // 记录初始旋转状态
                vsensPlatform.PlayAnimation(false);
                UpdateSliderPosition();
                rotationSlider.onValueChanged.AddListener(OnSliderValueChanged);
                rotationSlider.gameObject.SetActive(true);
            }
            else
            {
                rotationSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
                rotationSlider.gameObject.SetActive(false);
            }
        }

        public void OnBeginDrag()
        {
            if (!_toggle.isOn) return;
            _isDragging = true;
        }

        public void OnEndDrag()
        {
            if (!_toggle.isOn) return;
            _isDragging = false;
            // UpdateSliderPosition();
        }

        private void OnSliderValueChanged(float value)
        {
            if (!_isDragging || vsensPlatform.selectedSensor == null) return;
            
            // 应用局部旋转
            vsensPlatform.selectedSensor.transform.rotation = 
                Quaternion.AngleAxis(value, _worldAxis) * _initialRotation;
        }

        private Vector3 GetLocalAxis()
        {
            // 获取当前局部坐标系的轴
            switch (Axis)
            {
                case Axis.X: return vsensPlatform.selectedSensor.transform.up;
                case Axis.Y: return vsensPlatform.selectedSensor.transform.right;
                case Axis.Z: return vsensPlatform.selectedSensor.transform.forward;
                default: return Vector3.zero;
            }
        }
    }
}