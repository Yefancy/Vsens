using Animations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vsens.controls
{
    [RequireComponent(typeof(Toggle))]
    public class AnimationToggle : MonoBehaviour
    {
        public TextMeshProUGUI label;
        public RawAnimation rawAnimation;
        private Toggle _toggle;
        
        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
        }
        
        public void SetAnimation(RawAnimation rawAnimation)
        {
            this.rawAnimation = rawAnimation;
            if (label !=null)
            {
                label.text = rawAnimation.name;
            }
        }
    }
}