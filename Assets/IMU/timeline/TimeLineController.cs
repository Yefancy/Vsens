using TMPro;
using UnityEngine;

namespace IMU.timeline
{
    public class TimeLineController : MonoBehaviour
    {
        public TimelineGraphic timelineGraphic;
        public TextMeshProUGUI currentTimeText;

        private float _animationLength = 0f;
        private float _previewRange = 0f;
        private float _currentProgress = 0f;
        
        public void SetAnimationLength(float length)
        {
            if (Mathf.Approximately(_animationLength, length)) return;
            _animationLength = length;
            timelineGraphic.animationLength = length;
            timelineGraphic.SetAllDirty();
        }
        
        public void SetPreviewRange(float range)
        {
            if (Mathf.Approximately(_previewRange, range)) return;
            _previewRange = range;
            timelineGraphic.previewRange = range;
            timelineGraphic.SetAllDirty();
        }
        
        public void SetCurrentProgress(float progress)
        {
            if (Mathf.Approximately(_currentProgress, progress)) return;
            _currentProgress = progress;
            timelineGraphic.currentProgress = progress;
            timelineGraphic.SetAllDirty();
            currentTimeText.text = $"{progress * _animationLength:F2}";
        }
    }
}
