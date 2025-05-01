using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Animations;
using UnityEngine;
using UnityEngine.Networking;
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
            List<string> jsonPaths = new List<string>();
#if UNITY_ANDROID && !UNITY_EDITOR
            jsonPaths = new List<string>() { "knee_kick.json", "lunge_squat_twist.json", "yoga_a.json", "reverse_crunch.json", "reverse_lunge.json" };
#else
            if (Directory.Exists(animationPath))
            {
                var files = Directory.GetFiles(animationPath, "*.json");
                foreach (var file in files)
                {
                    jsonPaths.Add(file);
                }
            }
#endif
            foreach (var path in jsonPaths)
            {
                var animationName = Path.GetFileNameWithoutExtension(path);
                string fileContent = "";
#if UNITY_ANDROID && !UNITY_EDITOR
                using (UnityWebRequest www = UnityWebRequest.Get(Path.Combine(animationPath, path)))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        fileContent = www.downloadHandler.text;
                    }
                    else
                    {
                        Debug.LogError("Failed to load animation json: " + www.error);
                        continue;
                    }
                }
#else
                fileContent = File.ReadAllText(path);
#endif
                // loading animation async
                RawAnimation rawAnimation = null;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    rawAnimation = AnimationUtils.ParseSmplxAnimation(animationName, fileContent);
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
