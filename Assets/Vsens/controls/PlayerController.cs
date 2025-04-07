using UnityEngine;
using UnityEngine.UI;

namespace Vsens.controls
{
    public class PlayerController : MonoBehaviour
    {
        public VsensPlatform _platform;
        public Toggle playerToggle;
        public Button increaseRangeButton;
        public Button decreaseRangeButton;
        
        private void Awake()
        {
            playerToggle.SetIsOnWithoutNotify(_platform.IsPlaying());
            playerToggle.onValueChanged.AddListener(isOn =>
            {
                _platform.PlayAnimation(isOn);
            });
            increaseRangeButton.onClick.AddListener(() =>
            {
                var previewRange = _platform.PreviewRange;
                _platform.PreviewRange = Mathf.Clamp(previewRange + DynamicRange(previewRange + 0.01f), 0.01f, 1f);
            });
            decreaseRangeButton.onClick.AddListener(() =>
            {
                var previewRange = _platform.PreviewRange;
                _platform.PreviewRange = Mathf.Clamp(previewRange - DynamicRange(previewRange), 0.01f, 1f);
            });
        }

        private float DynamicRange(float currentRange)
        {
            if (currentRange <= 0.1f)
            {
                return 0.01f;
            }
            return 0.1f;
        }

        private void Update()
        {
            if (_platform.IsPlaying() != playerToggle.isOn)
            {
                playerToggle.isOn = _platform.IsPlaying();
            }
        }
    }
}
