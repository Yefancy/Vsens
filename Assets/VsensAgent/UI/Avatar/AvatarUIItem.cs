using System;
using System.Collections.Generic;
using com.convalise.UnityMaterialSymbols;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VsensAgent.Core;
using VsensAgent.RuntimeEditing;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.UI 
{
    public class AvatarUIItem : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        public TextMeshProUGUI avatarNameText;      // avatar名称
        public TextMeshProUGUI avatarStatusText;      // avatar状态
        public Toggle detailToggle;            
        public Image statusIndicator;                // 状态指示器
        public Image selectionHighlight;             // 选中高亮背景
        public MotionUIItem motionItemPrefab;             // motion prefab
        
        public GameObject detailContainer;       
        public Slider animationSlider;
        public Button playButton;
        public GameObject motionContainer;
        
        [Header("Display Settings")]
        public Color playingColor = Color.green;      // 活动状态颜色
        public Color pauseColor = Color.gray;     // 非活动状态颜色
        public Color selectedHighlightColor = Color.red;
        public Color unselectedHighlightColor = new Color(1f, 0f, 0f, 0f);
        public Color currentMotionItemColor = new Color(0.2f, 0.7f, 0.3f, 1f);
        public Color currentMotionTextColor = Color.white;
        public Color defaultMotionItemColor = new Color(1f, 1f, 1f, 0f);
        public Color defaultMotionTextColor = Color.white;
        
        // 私有变量
        private AvatarQueryModel avatar;
        private bool isInitialized = false;
        private RuntimeEditModeController cachedEditController;
        private AvatarRuntimeManager cachedAvatarRuntimeManager;
        private bool suppressSliderCallback;
        private bool isScrubbing;
        private readonly List<MotionUIItem> motionItems = new List<MotionUIItem>();
        private readonly List<string> cachedMotionCatalog = new List<string>();
        private string cachedSelectedMotionId = string.Empty;
        private MaterialSymbol _playButtonImage;

        public void Initialize(AvatarQueryModel avatarModel)
        {
            avatar = avatarModel;
            isInitialized = avatarModel != null;
            BindUiEvents();
            UpdateDisplay();
            UpdateSelectionHighlight();
        }

        public void UpdateAvatarModel(AvatarQueryModel avatarModel)
        {
            avatar = avatarModel;
            isInitialized = avatarModel != null;
            UpdateDisplay();
            UpdateSelectionHighlight();
        }

        public void UpdateDisplay()
        {
            if (!isInitialized || avatar == null)
            {
                if (avatarStatusText != null)
                {
                    avatarStatusText.text = "Avatar unavailable";
                }

                if (statusIndicator != null)
                {
                    statusIndicator.color = pauseColor;
                }

                RefreshPlayButtonColor(false);
                RefreshSlider(0f);

                return;
            }

            RefreshAvatarModelFromRuntime();

            if (avatarNameText != null)
            {
                avatarNameText.text = string.IsNullOrWhiteSpace(avatar.avatar_id) ? "avatar" : avatar.avatar_id;
            }

            if (statusIndicator != null)
            {
                statusIndicator.color = avatar.is_playing ? playingColor : pauseColor;
            }

            if (avatarStatusText != null)
            {
                avatarStatusText.text = BuildStatusText(detailToggle != null && detailToggle.isOn);
            }

            RefreshPlayButtonColor(avatar.is_playing);
            RefreshSlider(avatar.motion_progress);
            RefreshMotionList();
        }

        private string BuildStatusText(bool detailed)
        {
            if (avatar == null)
            {
                return "Avatar unavailable";
            }

            if (string.IsNullOrWhiteSpace(avatar.motion_id))
            {
                return "idle";
            }

            return avatar.is_playing ? "playing" : "pause";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || avatar == null)
            {
                return;
            }

            if (IsControlClick(eventData.pointerPressRaycast.gameObject) || IsControlClick(eventData.pointerCurrentRaycast.gameObject))
            {
                return;
            }

            TrySelectAvatarForEditing();
        }

        private bool IsControlClick(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            return (detailToggle != null && target.transform.IsChildOf(detailToggle.transform))
                   || (animationSlider != null && target.transform.IsChildOf(animationSlider.transform))
                   || (playButton != null && target.transform.IsChildOf(playButton.transform))
                   || (motionContainer != null && target.transform.IsChildOf(motionContainer.transform));
        }

        public void OnAnimationSliderPointerDown()
        {
            if (avatar == null)
            {
                return;
            }

            isScrubbing = true;
            var runtime = GetAvatarRuntimeManager();
            if (runtime != null)
            {
                runtime.TryPauseAvatarMotion(avatar.avatar_id, out _);
                RefreshAvatarModelFromRuntime();
                UpdateDisplay();
            }
        }

        public void OnAnimationSliderPointerUp()
        {
            isScrubbing = false;
        }

        public void SetMotionListVisibleForTests(bool visible)
        {
            if (detailContainer != null)
            {
                detailContainer.SetActive(visible);
            }

            if (detailToggle != null)
            {
                detailToggle.isOn = visible;
            }

            RefreshMotionList();
        }

        private void TrySelectAvatarForEditing()
        {
            var editController = GetEditController();
            if (editController == null || !editController.IsEditModeEnabled || avatar == null)
            {
                return;
            }

            editController.TrySelectEditableFromUi(avatar.avatar_id);
            UpdateSelectionHighlight();
        }

        private RuntimeEditModeController GetEditController()
        {
            if (cachedEditController != null)
            {
                return cachedEditController;
            }

            cachedEditController = ServiceLocator.IsRegistered<RuntimeEditModeController>()
                ? ServiceLocator.Get<RuntimeEditModeController>()
                : FindFirstObjectByType<RuntimeEditModeController>();
            return cachedEditController;
        }

        private AvatarRuntimeManager GetAvatarRuntimeManager()
        {
            if (cachedAvatarRuntimeManager != null)
            {
                return cachedAvatarRuntimeManager;
            }

            cachedAvatarRuntimeManager = ServiceLocator.IsRegistered<AvatarRuntimeManager>()
                ? ServiceLocator.Get<AvatarRuntimeManager>()
                : FindFirstObjectByType<AvatarRuntimeManager>();
            return cachedAvatarRuntimeManager;
        }

        private void TrySubscribeSelectionEvents()
        {
            var editController = GetEditController();
            if (editController == null)
            {
                return;
            }

            editController.SelectionChanged -= OnSelectionChanged;
            editController.SelectionChanged += OnSelectionChanged;
        }

        private void UnsubscribeSelectionEvents()
        {
            if (cachedEditController == null)
            {
                return;
            }

            cachedEditController.SelectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged(string _)
        {
            UpdateSelectionHighlight();
        }

        private void UpdateSelectionHighlight()
        {
            if (selectionHighlight == null)
            {
                return;
            }

            var editController = GetEditController();
            bool isSelected = avatar != null &&
                              editController != null &&
                              editController.IsEditModeEnabled &&
                              string.Equals(editController.SelectedObjectId, avatar.avatar_id, StringComparison.Ordinal);

            selectionHighlight.color = isSelected ? selectedHighlightColor : unselectedHighlightColor;
        }

        private void Awake()
        {
            if (selectionHighlight != null)
            {
                selectionHighlight.color = unselectedHighlightColor;
            }

            BindUiEvents();

            if (detailToggle != null && detailContainer != null)
            {
                detailToggle.isOn = detailContainer.activeSelf;
                detailToggle.onValueChanged.RemoveAllListeners();
                detailToggle.onValueChanged.AddListener(OnDetailToggleValueChanged);
            }
        }

        private void OnEnable()
        {
            TrySubscribeSelectionEvents();
            UpdateSelectionHighlight();
            UpdateDisplay();
        }

        private void OnDisable()
        {
            UnsubscribeSelectionEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeSelectionEvents();
        }

        private void OnDetailToggleValueChanged(bool isOn)
        {
            if (detailContainer != null)
            {
                detailContainer.SetActive(isOn);
            }

            UpdateDisplay();
        }

        private void BindUiEvents()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayButtonClicked);
                playButton.onClick.AddListener(OnPlayButtonClicked);
            }

            if (animationSlider != null)
            {
                animationSlider.minValue = 0f;
                animationSlider.maxValue = 1f;
                animationSlider.onValueChanged.RemoveListener(OnAnimationSliderValueChanged);
                animationSlider.onValueChanged.AddListener(OnAnimationSliderValueChanged);

                var trigger = animationSlider.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = animationSlider.gameObject.AddComponent<EventTrigger>();
                }

                BindEventTrigger(trigger, EventTriggerType.PointerDown, _ => OnAnimationSliderPointerDown());
                BindEventTrigger(trigger, EventTriggerType.PointerUp, _ => OnAnimationSliderPointerUp());
            }
        }

        private static void BindEventTrigger(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> callback)
        {
            for (int i = trigger.triggers.Count - 1; i >= 0; i--)
            {
                if (trigger.triggers[i].eventID == type)
                {
                    trigger.triggers.RemoveAt(i);
                }
            }

            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => callback(data));
            trigger.triggers.Add(entry);
        }

        private void RefreshAvatarModelFromRuntime()
        {
            if (avatar == null)
            {
                return;
            }

            var runtime = GetAvatarRuntimeManager();
            if (runtime == null)
            {
                return;
            }

            var avatars = runtime.GetAvatarQueryModels(null);
            var runtimeAvatar = avatars.Find(model => string.Equals(model.avatar_id, avatar.avatar_id, StringComparison.Ordinal));
            if (runtimeAvatar != null)
            {
                avatar = runtimeAvatar;
            }
        }

        private void RefreshPlayButtonColor(bool isPlaying)
        {
            if (playButton == null)
            {
                return;
            }

            if (_playButtonImage == null)
            {
                _playButtonImage = playButton.GetComponent<MaterialSymbol>();
            }
            if (_playButtonImage != null)
            {
                _playButtonImage.color = isPlaying ? playingColor : Color.white;
            }
            
        }

        private void RefreshSlider(float normalizedProgress)
        {
            if (animationSlider == null || isScrubbing)
            {
                return;
            }

            suppressSliderCallback = true;
            animationSlider.SetValueWithoutNotify(Mathf.Clamp01(normalizedProgress));
            suppressSliderCallback = false;
        }

        private void OnPlayButtonClicked()
        {
            if (avatar == null)
            {
                return;
            }

            var runtime = GetAvatarRuntimeManager();
            if (runtime == null)
            {
                return;
            }

            if (avatar.is_playing)
            {
                runtime.TryPauseAvatarMotion(avatar.avatar_id, out _);
            }
            else
            {
                runtime.TryPlayAvatarMotion(avatar.avatar_id, 1f, true, out _);
            }

            RefreshAvatarModelFromRuntime();
            UpdateDisplay();
        }

        private void OnAnimationSliderValueChanged(float normalizedProgress)
        {
            if (suppressSliderCallback || avatar == null)
            {
                return;
            }

            var runtime = GetAvatarRuntimeManager();
            if (runtime == null)
            {
                return;
            }

            runtime.TrySetAvatarMotionNormalizedProgress(avatar.avatar_id, normalizedProgress, out _);
            RefreshAvatarModelFromRuntime();
            UpdateDisplay();
        }

        private void RefreshMotionList()
        {
            if (motionContainer == null || motionItemPrefab == null || detailContainer == null || !detailContainer.activeSelf)
            {
                return;
            }

            var runtime = GetAvatarRuntimeManager();
            if (runtime == null)
            {
                return;
            }

            var motions = runtime.GetMotionQueryModels();
            if (ShouldRebuildMotionList(motions))
            {
                RebuildMotionList(motions);
            }

            UpdateMotionListSelection(motions);
        }

        private void ClearMotionList()
        {
            for (int i = 0; i < motionItems.Count; i++)
            {
                if (motionItems[i] != null)
                {
                    DestroyImmediate(motionItems[i].gameObject);
                }
            }

            motionItems.Clear();
            cachedMotionCatalog.Clear();
            cachedSelectedMotionId = string.Empty;
        }

        private bool ShouldRebuildMotionList(List<AvatarMotionQueryModel> motions)
        {
            if (motions.Count != motionItems.Count || motions.Count != cachedMotionCatalog.Count)
            {
                return true;
            }

            for (int i = 0; i < motions.Count; i++)
            {
                string key = BuildMotionCacheKey(motions[i]);
                if (!string.Equals(cachedMotionCatalog[i], key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildMotionList(List<AvatarMotionQueryModel> motions)
        {
            ClearMotionList();
            foreach (var motion in motions)
            {
                motionItems.Add(CreateMotionListItem(motion));
                cachedMotionCatalog.Add(BuildMotionCacheKey(motion));
            }
        }

        private void UpdateMotionListSelection(List<AvatarMotionQueryModel> motions)
        {
            string selectedMotionId = avatar?.motion_id ?? string.Empty;
            if (string.Equals(cachedSelectedMotionId, selectedMotionId, StringComparison.Ordinal) && motions.Count == motionItems.Count)
            {
                return;
            }

            cachedSelectedMotionId = selectedMotionId;

            for (int i = 0; i < motions.Count && i < motionItems.Count; i++)
            {
                bool isCurrent = string.Equals(selectedMotionId, motions[i].motion_id, StringComparison.Ordinal);
                motionItems[i].SetVisualState(
                    string.IsNullOrWhiteSpace(motions[i].motion_name) ? motions[i].motion_id : motions[i].motion_name,
                    isCurrent,
                    currentMotionItemColor,
                    defaultMotionItemColor,
                    currentMotionTextColor,
                    defaultMotionTextColor);
            }
        }

        private static string BuildMotionCacheKey(AvatarMotionQueryModel motion)
        {
            return $"{motion.motion_id}|{motion.motion_name}";
        }

        private MotionUIItem CreateMotionListItem(AvatarMotionQueryModel motion)
        {
            var motionItemGo = Instantiate(motionItemPrefab.gameObject, motionContainer.transform);
            motionItemGo.SetActive(true);
            var motionItem = motionItemGo.GetComponent<MotionUIItem>();
            motionItem.name = $"Motion_{motion.motion_id}";
            bool isCurrent = avatar != null && string.Equals(avatar.motion_id, motion.motion_id, StringComparison.Ordinal);
            motionItem.Initialize(
                string.IsNullOrWhiteSpace(motion.motion_name) ? motion.motion_id : motion.motion_name,
                isCurrent,
                currentMotionItemColor,
                defaultMotionItemColor,
                currentMotionTextColor,
                defaultMotionTextColor,
                () => OnMotionItemClicked(motion.motion_id));

            return motionItem;
        }

        private void OnMotionItemClicked(string motionId)
        {
            if (avatar == null)
            {
                return;
            }

            var runtime = GetAvatarRuntimeManager();
            if (runtime == null)
            {
                return;
            }

            runtime.TrySwitchAvatarMotion(avatar.avatar_id, motionId, true, out _);
            RefreshAvatarModelFromRuntime();
            UpdateDisplay();
        }

    }

}
