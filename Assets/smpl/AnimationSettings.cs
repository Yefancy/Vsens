    using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Animations;
using UnityEngine;
using UnityEngine.UI;
using Toggle = UnityEngine.UI.Toggle;

namespace smpl
{
    public class AnimationSettings : MonoBehaviour
    {
        [SerializeField] private ToggleGroup toggleGroup;
        [SerializeField] private PrefabPreview prefabPreview;
        [SerializeField] private BodyAnimationController bodyAnimationController;
        [SerializeField] private GameObject previewModel;
        
        // runtime
        public readonly List<RawAnimation> animations = new();
        public RawAnimation selectedAnimation
        {
            get;
            private set;
        }
        
        
        // Start is called before the first frame update
        void Start()
        {
            StartCoroutine(UpdateAnimationView());
        }

        private IEnumerator UpdateAnimationView()
        {
            var animationPath = Path.Combine(Application.streamingAssetsPath, "animations");
            if (!Directory.Exists(animationPath))
            {
                Directory.CreateDirectory(animationPath);
            }
            var files = Directory.GetFiles(animationPath, "*.json");
            foreach (var file in files)
            {
                var animationName = Path.GetFileNameWithoutExtension(file);
                // loading animation async
                RawAnimation rawAnimation = null;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    rawAnimation = AnimationUtils.ParseSmplAnimation(animationName, File.ReadAllText(file));
                });
                yield return new WaitUntil(() => rawAnimation != null);
                animations.Add(rawAnimation);
                
                var copiedToggle = Instantiate(prefabPreview, toggleGroup.transform);
                copiedToggle.gameObject.SetActive(true);
                copiedToggle.prefabName.text = animationName;
                if (previewModel != null)
                {
                    Texture2D preview = null;
                    var animationController = previewModel.GetComponent<BodyAnimationController>();
                    if (animationController != null)
                    {
                        animationController.PrepareModel();
                        animationController.setAnimation(rawAnimation);
                        animationController.RunNextFrame(0.001f);
                        yield return copiedToggle.RenderingPreview(previewModel);
                    }
                }
                if (copiedToggle.TryGetComponent<Toggle>(out var toggle))
                {
                    toggle.onValueChanged.AddListener(isOn =>
                    {
                        if (isOn)
                        {
                            selectedAnimation = rawAnimation;
                            bodyAnimationController.setAnimation(rawAnimation);
                            bodyAnimationController.time = 0;
                        }
                        else if (selectedAnimation == rawAnimation)
                        {
                            selectedAnimation = null;
                            bodyAnimationController.removeAnimation();
                        }
                    });
                }
            }
        }
    }
}
