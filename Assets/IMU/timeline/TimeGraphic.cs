using UnityEngine;
using UnityEngine.UI;

namespace IMU.timeline
{
    public class TimelineGraphic : Graphic
    {
        public float canvasWidth = 2000f;
        public float animationLength = 10f;
        [Range(0, 1)] public float previewRange = 1f;
        [Range(0, 1)] public float currentProgress = 0.5f;

        public float majorTickHeight = 60f;
        public float minorTickHeight = 30f;
        public float tickWidth = 2f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float visibleTimeRange = previewRange * animationLength;

            float currentTime = currentProgress * animationLength;
            float startTime = currentTime - visibleTimeRange / 2f;
            float endTime = currentTime + visibleTimeRange / 2f;

            var pixelWidth = canvasWidth / visibleTimeRange;
            var current = currentTime;
            while (current <= endTime)
            {
                var t = Mathf.Floor(current * 5f) / 5f;
                bool isMajor = Mathf.Approximately(t % 1f, 0f);
                float relativeT = t - currentTime;
                float x = canvasWidth / 2 + relativeT * pixelWidth;
                float height = isMajor ? majorTickHeight : minorTickHeight;
                AddRect(vh, new Vector2(x, (1 - height) / 2f), tickWidth, height, color);
                current += 0.2f;
            }
            current = currentTime;
            while (current >= startTime)
            {
                var t = Mathf.Ceil(current * 5f) / 5f;
                bool isMajor = Mathf.Approximately(t % 1f, 0f);
                float relativeT = currentTime - t;
                float x = canvasWidth / 2 - relativeT * pixelWidth;
                float height = isMajor ? majorTickHeight : minorTickHeight;
                AddRect(vh, new Vector2(x, (1 - height) / 2f), tickWidth, height, color);
                current -= 0.2f;
            }
        }

        private void AddRect(VertexHelper vh, Vector2 pos, float width, float height, Color32 col)
        {
            int idx = vh.currentVertCount;

            vh.AddVert(pos + new Vector2(-width / 2, 0), col, Vector2.zero);
            vh.AddVert(pos + new Vector2(-width / 2, height), col, Vector2.zero);
            vh.AddVert(pos + new Vector2(width / 2, height), col, Vector2.zero);
            vh.AddVert(pos + new Vector2(width / 2, 0), col, Vector2.zero);

            vh.AddTriangle(idx + 0, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx + 0);
        }
    }
}