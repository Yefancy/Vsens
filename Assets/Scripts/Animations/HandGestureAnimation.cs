using OVRSimpleJSON;
using UnityEngine;

namespace Animations
{
    public class HandGestureAnimation
    {
        private HandGestureKeyFrame[] lefHandGestureKeyFrames;
        private HandGestureKeyFrame[] rightHandGestureKeyFrames;
        private float duration;
        
        public HandGestureKeyFrame[] LefHandGestureKeyFrames => lefHandGestureKeyFrames;
        public HandGestureKeyFrame[] RightHandGestureKeyFrames => rightHandGestureKeyFrames;
        public float Duration => duration;
        
        public HandGestureAnimation(HandGestureKeyFrame[] lefHandGestureKeyFrames, HandGestureKeyFrame[] rightHandGestureKeyFrames)
        {
            this.lefHandGestureKeyFrames = lefHandGestureKeyFrames;
            this.rightHandGestureKeyFrames = rightHandGestureKeyFrames;
            duration = Mathf.Max(lefHandGestureKeyFrames[^1].time, rightHandGestureKeyFrames[^1].time);
        }
        
        public JSONObject serialize()
        {
            var json = new JSONObject();
            var leftHandKeyFrames = new JSONArray();
            foreach (var keyFrame in lefHandGestureKeyFrames)
            {
                leftHandKeyFrames.Add(keyFrame.serialize());
            }
            json["leftHandGestureKeyFrames"] = leftHandKeyFrames;
            var rightHandKeyFrames = new JSONArray();
            foreach (var keyFrame in rightHandGestureKeyFrames)
            {
                rightHandKeyFrames.Add(keyFrame.serialize());
            }
            json["rightHandGestureKeyFrames"] = rightHandKeyFrames;
            return json;
        }
    }
}