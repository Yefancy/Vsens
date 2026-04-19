using UnityEngine;
using UnityEngine.EventSystems;
using VsensAgent.Core;

namespace VsensAgent.UI
{
    public sealed class StatusHintTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string hintText;
        [SerializeField] private UniversalStatusBar statusBar;

        public void SetHint(string hint)
        {
            hintText = hint;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ResolveStatusBar()?.SetHoverHint(hintText);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResolveStatusBar()?.ClearHoverHint(hintText);
        }

        private UniversalStatusBar ResolveStatusBar()
        {
            if (statusBar != null)
            {
                return statusBar;
            }

            statusBar = ServiceLocator.IsRegistered<UniversalStatusBar>()
                ? ServiceLocator.Get<UniversalStatusBar>()
                : FindFirstObjectByType<UniversalStatusBar>();
            return statusBar;
        }
    }
}
