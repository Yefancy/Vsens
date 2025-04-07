using System;
using System.Collections;
using System.IO;
using System.Threading;
using Animations;
using UnityEngine;
using UnityEngine.UI;

namespace Vsens.controls
{
    [RequireComponent(typeof(ToggleGroup))]
    public class AnimationController : MonoBehaviour
    {
        public VsensPlatform VsensPlatform;
        public AnimationToggle templateToggle;
        private ToggleGroup _toggleGroup;

        private void Awake()
        {
            _toggleGroup = GetComponent<ToggleGroup>();
        }

        private void Start()
        {
            StartCoroutine(LoadAnimations());
        }
        
        private IEnumerator LoadAnimations()
        {
            // find animation files under the StreamingAssets folder
            var animationPath = Path.Combine(Application.streamingAssetsPath, "HumanMotion");
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
                    rawAnimation = AnimationUtils.ParseSmplxAnimation(animationName, File.ReadAllText(file));
                });
                yield return new WaitUntil(() => rawAnimation != null);
                AddAnimation(rawAnimation);
            }
        }

        public void AddAnimation(RawAnimation rawAnimation)
        {
            var animationToggle = Instantiate(templateToggle, templateToggle.transform.parent);
            animationToggle.gameObject.SetActive(true);
            animationToggle.SetAnimation(rawAnimation);
            animationToggle.GetComponent<Toggle>().group = _toggleGroup;
            animationToggle.GetComponent<Toggle>().onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                {
                    VsensPlatform.SetAnimation(rawAnimation);
                }
            });
        }

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
            VsensPlatform.ApplyToAllActor((index, actor) =>
            {
                if (index < VsensPlatform.actors.Length - 1) actor.gameObject.SetActive(!isActive);
            });
        }

    }
}
