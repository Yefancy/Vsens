using UnityEngine;
using UnityEngine.UI;

namespace Vsens.ui
{
    [RequireComponent(typeof(Toggle))]
    public class ToggleSpriteSwap : MonoBehaviour
    {
        private Toggle _toggle;
        
        public Image targetImage;
        [SerializeField] private Sprite onSprite;
        [SerializeField] private Sprite offSprite;
        
        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
            if (_toggle == null) return;
            _toggle.onValueChanged.AddListener(OnToggleValueChanged);
            OnToggleValueChanged(_toggle.isOn);
        }
        
        private void OnToggleValueChanged(bool isOn)
        {
            if (targetImage == null) return;
            targetImage.sprite = isOn ? onSprite : offSprite;
        }
    }
}
