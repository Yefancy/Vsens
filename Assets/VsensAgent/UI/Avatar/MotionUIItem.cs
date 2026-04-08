using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VsensAgent.UI 
{
    public class MotionUIItem : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        public TextMeshProUGUI motionNameText;      // motion名称
        public Image backgroundImage;               // background

        private UnityEngine.Events.UnityAction clickAction;

        public void Initialize(string motionName, bool isSelected, Color selectedColor, Color defaultColor, Color selectedTextColor, Color defaultTextColor, UnityEngine.Events.UnityAction onClick)
        {
            clickAction = onClick;
            SetVisualState(motionName, isSelected, selectedColor, defaultColor, selectedTextColor, defaultTextColor);
        }

        public void SetVisualState(string motionName, bool isSelected, Color selectedColor, Color defaultColor, Color selectedTextColor, Color defaultTextColor)
        {
            if (motionNameText != null)
            {
                motionNameText.text = motionName;
                motionNameText.color = isSelected ? selectedTextColor : defaultTextColor;
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = isSelected ? selectedColor : defaultColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            clickAction?.Invoke();
        }
    }
}
