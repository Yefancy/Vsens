using Oculus.Interaction;
using UnityEngine;

namespace smpl
{
    public class AnimationModel : PointableElement
    {
        [SerializeField] private ARCanvasFeature arCanvas;
        
        public override void ProcessPointerEvent(PointerEvent evt)
        {
            base.ProcessPointerEvent(evt);
            if (evt.Type == PointerEventType.Select)
            {
                if (arCanvas.IsOpen)
                {
                    arCanvas.CloseCanvas();
                } else
                {
                    arCanvas.OpenCanvas();
                }
            }
        }
    }
}
