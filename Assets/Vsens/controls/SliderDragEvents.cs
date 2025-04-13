using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Vsens.controls
{
    [RequireComponent(typeof(Slider))]
    public class SliderDragEvents : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public UnityEvent onBeginDrag = new UnityEvent();
        public UnityEvent onEndDrag = new UnityEvent();

        public void OnBeginDrag(PointerEventData eventData)
        {
            onBeginDrag.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            onEndDrag.Invoke();
        }
    }
}