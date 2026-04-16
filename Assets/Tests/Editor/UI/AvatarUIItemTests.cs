using com.convalise.UnityMaterialSymbols;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VsensAgent.Core;
using VsensAgent.SceneApi.V2;
using VsensAgent.Tests.Editor.SceneApi;
using VsensAgent.UI;

namespace VsensAgent.Tests.Editor.UI
{
    public class AvatarUIItemTests
    {
        [Test]
        public void PlayButton_TogglesPauseAndUpdatesButtonColor()
        {
            ServiceLocator.Clear();
            var runtimeRoot = new GameObject("AvatarUiRuntimeRoot");
            var prefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();
            var itemGo = CreateAvatarItemPrefab();

            try
            {
                var runtime = runtimeRoot.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_wave",
                    "wave_once",
                    "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":30.0,\"betas\":[],\"poses\":[],\"trans\":[[0,0,0]]}",
                    "wave once",
                    out var loadError), Is.True, loadError);
                Assert.That(runtime.TryPlayAvatarMotion("avatar_main", 1f, true, out var playError), Is.True, playError);

                var item = itemGo.GetComponent<AvatarUIItem>();
                item.Initialize(runtime.GetAvatarQueryModels(null)[0]);
                item.UpdateDisplay();

                var playButtonImage = item.playButton.GetComponent<MaterialSymbol>();
                Assert.That(playButtonImage.color, Is.EqualTo(Color.green));

                item.playButton.onClick.Invoke();
                item.UpdateDisplay();

                Assert.That(runtime.GetAvatarQueryModels(null)[0].is_playing, Is.False);
                Assert.That(playButtonImage.color, Is.EqualTo(Color.white));

                item.playButton.onClick.Invoke();
                item.UpdateDisplay();

                Assert.That(runtime.GetAvatarQueryModels(null)[0].is_playing, Is.True);
                Assert.That(playButtonImage.color, Is.EqualTo(Color.green));
            }
            finally
            {
                Object.DestroyImmediate(itemGo);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(runtimeRoot);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void AnimationSlider_ScrubPausesAndSetsNormalizedProgress()
        {
            ServiceLocator.Clear();
            var runtimeRoot = new GameObject("AvatarUiSliderRuntimeRoot");
            var prefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();
            var itemGo = CreateAvatarItemPrefab();

            try
            {
                var runtime = runtimeRoot.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_wave",
                    "wave_once",
                    "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":30.0,\"betas\":[],\"poses\":[],\"trans\":[[0,0,0],[1,0,0]]}",
                    "wave once",
                    out var loadError), Is.True, loadError);
                Assert.That(runtime.TryPlayAvatarMotion("avatar_main", 1f, true, out var playError), Is.True, playError);

                var item = itemGo.GetComponent<AvatarUIItem>();
                item.Initialize(runtime.GetAvatarQueryModels(null)[0]);

                item.OnAnimationSliderPointerDown();
                item.animationSlider.value = 0.5f;

                var avatar = runtime.GetAvatarQueryModels(null)[0];
                Assert.That(avatar.is_playing, Is.False);
                Assert.That(avatar.motion_progress, Is.EqualTo(0.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(itemGo);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(runtimeRoot);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void MotionListItem_ClickLoadsMotionAndImmediatelyPlays()
        {
            ServiceLocator.Clear();
            var runtimeRoot = new GameObject("AvatarUiMotionListRuntimeRoot");
            var prefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();
            var itemGo = CreateAvatarItemPrefab();
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();

            try
            {
                var runtime = runtimeRoot.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_wave",
                    "wave_once",
                    "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":30.0,\"betas\":[],\"poses\":[],\"trans\":[[0,0,0]]}",
                    "wave once",
                    out var loadWaveError), Is.True, loadWaveError);
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_walk",
                    "walk_forward",
                    "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":30.0,\"betas\":[],\"poses\":[],\"trans\":[[0,0,0],[0,0,1]]}",
                    "walk forward",
                    out var loadWalkError), Is.True, loadWalkError);

                var item = itemGo.GetComponent<AvatarUIItem>();
                item.Initialize(runtime.GetAvatarQueryModels(null)[0]);
                item.SetMotionListVisibleForTests(true);

                var motionItems = item.motionContainer.GetComponentsInChildren<MotionUIItem>(true);
                Assert.That(motionItems.Length, Is.GreaterThanOrEqualTo(2));

                motionItems[0].OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });

                var avatar = runtime.GetAvatarQueryModels(null)[0];
                Assert.That(avatar.motion_id, Is.EqualTo("motion_wave"));
                Assert.That(avatar.motion_name, Is.EqualTo("wave_once"));
                Assert.That(avatar.is_playing, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(eventSystemGo);
                Object.DestroyImmediate(itemGo);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(runtimeRoot);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void AvatarQueryMotionProgress_WrapsWhenLoopingPastEnd()
        {
            ServiceLocator.Clear();
            var runtimeRoot = new GameObject("AvatarUiLoopRuntimeRoot");
            var prefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();

            try
            {
                var runtime = runtimeRoot.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_wave",
                    "wave_once",
                    "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":30.0,\"betas\":[],\"poses\":[],\"trans\":[[0,0,0]]}",
                    "wave once",
                    out var loadError), Is.True, loadError);
                Assert.That(runtime.TryPlayAvatarMotion("avatar_main", 1f, true, out var playError), Is.True, playError);

                var driver = runtime.GetManagedAvatarObject().GetComponent<TestAvatarPlaybackDriver>();
                driver.reportedProgress = 1.25f;

                var avatar = runtime.GetAvatarQueryModels(null)[0];
                Assert.That(avatar.motion_progress, Is.EqualTo(0.25f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(runtimeRoot);
                ServiceLocator.Clear();
            }
        }

        private static GameObject CreateAvatarItemPrefab()
        {
            var root = new GameObject("AvatarItemPrefab");
            var item = root.AddComponent<AvatarUIItem>();

            item.avatarNameText = new GameObject("AvatarName").AddComponent<TextMeshProUGUI>();
            item.avatarNameText.transform.SetParent(root.transform, false);

            item.avatarStatusText = new GameObject("AvatarStatus").AddComponent<TextMeshProUGUI>();
            item.avatarStatusText.transform.SetParent(root.transform, false);

            item.statusIndicator = new GameObject("StatusIndicator").AddComponent<Image>();
            item.statusIndicator.transform.SetParent(root.transform, false);

            item.selectionHighlight = new GameObject("SelectionHighlight").AddComponent<Image>();
            item.selectionHighlight.transform.SetParent(root.transform, false);

            item.detailContainer = new GameObject("DetailContainer");
            item.detailContainer.transform.SetParent(root.transform, false);
            item.detailContainer.SetActive(false);

            var toggleGo = new GameObject("DetailToggle");
            toggleGo.transform.SetParent(root.transform, false);
            item.detailToggle = toggleGo.AddComponent<Toggle>();

            var sliderGo = new GameObject("AnimationSlider");
            sliderGo.transform.SetParent(item.detailContainer.transform, false);
            item.animationSlider = sliderGo.AddComponent<Slider>();
            item.animationSlider.minValue = 0f;
            item.animationSlider.maxValue = 1f;

            var playButtonGo = new GameObject("PlayButton");
            playButtonGo.transform.SetParent(item.detailContainer.transform, false);
            var playButtonImage = playButtonGo.AddComponent<Image>();
            playButtonImage.color = Color.white;
            item.playButton = playButtonGo.AddComponent<Button>();

            item.motionContainer = new GameObject("MotionContainer");
            item.motionContainer.transform.SetParent(item.detailContainer.transform, false);
            item.motionItemPrefab = CreateMotionItemPrefab().GetComponent<MotionUIItem>();

            return root;
        }

        private static GameObject CreateMotionItemPrefab()
        {
            var root = new GameObject("MotionItemPrefab");
            var motionItem = root.AddComponent<MotionUIItem>();

            motionItem.backgroundImage = root.AddComponent<Image>();

            var labelGo = new GameObject("MotionName");
            labelGo.transform.SetParent(root.transform, false);
            motionItem.motionNameText = labelGo.AddComponent<TextMeshProUGUI>();

            return root;
        }
    }
}
