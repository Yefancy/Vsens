using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VsensAgent.Core;
using VsensAgent.SceneHistory;

namespace VsensAgent.UI
{
    public sealed class OperationHistoryTabView : MonoBehaviour
    {
        [SerializeField] private ChatUIManager chatUiManager;
        [SerializeField] private SceneActionHistory sceneActionHistory;
        [SerializeField] private RectTransform tabBar;
        [SerializeField] private Button chatTabButton;
        [SerializeField] private Button actionsTabButton;
        [SerializeField] private ScrollRect actionScrollRect;
        [SerializeField] private RectTransform actionContainer;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private int maxVisibleRecords = 200;
        [SerializeField] private bool manageTabButtonColors;

        private bool showingActions;

        private static readonly Color GeneratedActiveTabColor = new(0.20f, 0.58f, 0.44f, 0.95f);
        private static readonly Color GeneratedInactiveTabColor = new(0.08f, 0.08f, 0.08f, 0.85f);
        private static readonly Color PanelColor = new(0f, 0f, 0f, 0.82f);
        private static readonly Color RowColor = new(0.05f, 0.05f, 0.05f, 0.55f);

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            ResolveReferences();
            EnsureUi();
            BindButtons();
            SubscribeHistory();
            ShowChat();
        }

        private void OnEnable()
        {
            BindButtons();
            SubscribeHistory();
            RebuildFromHistory();
        }

        private void OnDisable()
        {
            if (chatTabButton != null)
            {
                chatTabButton.onClick.RemoveListener(ShowChat);
            }

            if (actionsTabButton != null)
            {
                actionsTabButton.onClick.RemoveListener(ShowActions);
            }

            if (sceneActionHistory != null)
            {
                sceneActionHistory.OperationRecorded -= OnOperationRecorded;
            }
        }

        public void ShowChat()
        {
            showingActions = false;
            ApplyViewState();
        }

        public void ShowActions()
        {
            showingActions = true;
            RefreshOperationHistory();
            ApplyViewState();
        }

        public void RefreshOperationHistory()
        {
            ResolveReferences();
            RebuildFromHistory();
        }

        private void ResolveReferences()
        {
            chatUiManager ??= GetComponent<ChatUIManager>() ?? GetComponentInParent<ChatUIManager>();
            sceneActionHistory ??= GetComponentInParent<SceneActionHistory>() ??
                                   GetComponentInChildren<SceneActionHistory>(true) ??
                                   (ServiceLocator.IsRegistered<SceneActionHistory>()
                                       ? ServiceLocator.Get<SceneActionHistory>()
                                       : FindFirstObjectByType<SceneActionHistory>());
            fontAsset ??= TMP_Settings.defaultFontAsset;
        }

        private void EnsureUi()
        {
            EnsureTabBar();
            EnsureActionScrollArea();
        }

        private void EnsureTabBar()
        {
            if (tabBar == null)
            {
                tabBar = transform.Find("TabBar") as RectTransform;
            }

            if (tabBar == null)
            {
                var tabBarObject = new GameObject("TabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                tabBarObject.transform.SetParent(transform, false);
                tabBar = tabBarObject.GetComponent<RectTransform>();
            }

            tabBar.anchorMin = new Vector2(0f, 1f);
            tabBar.anchorMax = new Vector2(1f, 1f);
            tabBar.pivot = new Vector2(0.5f, 1f);
            tabBar.offsetMin = new Vector2(10f, -44f);
            tabBar.offsetMax = new Vector2(-10f, -10f);

            var layout = tabBar.GetComponent<HorizontalLayoutGroup>() ?? tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            chatTabButton = EnsureTabButton("ChatTab", "Chat", chatTabButton);
            actionsTabButton = EnsureTabButton("ActionsTab", "Actions", actionsTabButton);
            tabBar.SetAsFirstSibling();
        }

        private Button EnsureTabButton(string objectName, string labelText, Button current)
        {
            if (current == null)
            {
                var existing = tabBar.Find(objectName);
                current = existing != null ? existing.GetComponent<Button>() : null;
            }

            if (current == null)
            {
                var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                buttonObject.transform.SetParent(tabBar, false);
                current = buttonObject.GetComponent<Button>();
                var generatedImage = current.GetComponent<Image>();
                if (generatedImage != null)
                {
                    generatedImage.color = GeneratedInactiveTabColor;
                }
            }

            var layout = current.GetComponent<LayoutElement>() ?? current.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 96f;
            layout.minWidth = 84f;

            var image = current.GetComponent<Image>() ?? current.gameObject.AddComponent<Image>();
            current.targetGraphic = image;

            var label = current.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(current.transform, false);
                label = labelObject.GetComponent<TextMeshProUGUI>();
            }

            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.text = labelText;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 16f;
            label.color = Color.white;
            label.raycastTarget = false;
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            return current;
        }

        private void EnsureActionScrollArea()
        {
            if (actionScrollRect == null)
            {
                var existing = transform.Find("ActionScrollArea");
                actionScrollRect = existing != null ? existing.GetComponent<ScrollRect>() : null;
            }

            if (actionScrollRect == null)
            {
                var scrollObject = new GameObject("ActionScrollArea", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
                scrollObject.transform.SetParent(transform, false);
                actionScrollRect = scrollObject.GetComponent<ScrollRect>();

                var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewportObject.transform.SetParent(scrollObject.transform, false);
                var viewportImage = viewportObject.GetComponent<Image>();
                viewportImage.color = Color.clear;
                viewportImage.raycastTarget = true;
                viewportObject.GetComponent<Mask>().showMaskGraphic = false;

                var contentObject = new GameObject("ActionContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentObject.transform.SetParent(viewportObject.transform, false);
                actionContainer = contentObject.GetComponent<RectTransform>();
                actionScrollRect.viewport = viewportObject.GetComponent<RectTransform>();
                actionScrollRect.content = actionContainer;
            }
            else
            {
                if (actionScrollRect.viewport == null)
                {
                    var viewport = actionScrollRect.transform.Find("Viewport") as RectTransform;
                    actionScrollRect.viewport = viewport;
                }

                if (actionScrollRect.content == null && actionScrollRect.viewport != null)
                {
                    var content = actionScrollRect.viewport.Find("ActionContainer") as RectTransform;
                    actionScrollRect.content = content;
                }
            }

            var scrollRectTransform = actionScrollRect.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(10f, 10f);
            scrollRectTransform.offsetMax = new Vector2(-10f, -54f);

            var panelImage = actionScrollRect.GetComponent<Image>() ?? actionScrollRect.gameObject.AddComponent<Image>();
            panelImage.color = PanelColor;
            actionScrollRect.horizontal = false;
            actionScrollRect.vertical = true;
            actionScrollRect.movementType = ScrollRect.MovementType.Clamped;

            if (actionScrollRect.viewport != null)
            {
                var viewportRect = actionScrollRect.viewport;
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = new Vector2(8f, 8f);
                viewportRect.offsetMax = new Vector2(-8f, -8f);
            }

            actionContainer ??= actionScrollRect.content;
            if (actionContainer != null)
            {
                actionContainer.anchorMin = new Vector2(0f, 1f);
                actionContainer.anchorMax = new Vector2(1f, 1f);
                actionContainer.pivot = new Vector2(0.5f, 1f);
                actionContainer.offsetMin = new Vector2(0f, 0f);
                actionContainer.offsetMax = new Vector2(0f, 0f);

                var layout = actionContainer.GetComponent<VerticalLayoutGroup>() ?? actionContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(8, 8, 8, 8);
                layout.spacing = 6f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                var fitter = actionContainer.GetComponent<ContentSizeFitter>() ?? actionContainer.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }

        private void BindButtons()
        {
            if (chatTabButton != null)
            {
                chatTabButton.onClick.RemoveListener(ShowChat);
                chatTabButton.onClick.AddListener(ShowChat);
            }

            if (actionsTabButton != null)
            {
                actionsTabButton.onClick.RemoveListener(ShowActions);
                actionsTabButton.onClick.AddListener(ShowActions);
            }
        }

        private void SubscribeHistory()
        {
            sceneActionHistory ??= SceneActionHistory.GetOrCreate();
            if (sceneActionHistory == null)
            {
                return;
            }

            sceneActionHistory.OperationRecorded -= OnOperationRecorded;
            sceneActionHistory.OperationRecorded += OnOperationRecorded;
        }

        private void RebuildFromHistory()
        {
            if (sceneActionHistory == null || actionContainer == null)
            {
                return;
            }

            ClearRows();
            foreach (var record in sceneActionHistory.OperationLog)
            {
                AddRecordRow(record);
            }

            ScrollActionsToBottom();
        }

        private void OnOperationRecorded(SceneActionRecord record)
        {
            AddRecordRow(record);
            TrimRows();
            ScrollActionsToBottom();
        }

        private void AddRecordRow(SceneActionRecord record)
        {
            if (record == null || actionContainer == null)
            {
                return;
            }

            var row = new GameObject("ActionRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(actionContainer, false);
            row.GetComponent<Image>().color = RowColor;
            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 34f;
            layout.preferredHeight = 42f;

            var labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(row.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = 14f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            if (fontAsset != null)
            {
                label.font = fontAsset;
            }

            label.text = FormatRecord(record);
        }

        private string FormatRecord(SceneActionRecord record)
        {
            string timestamp = FormatTimestamp(record.timestampUtc);
            string source = string.IsNullOrWhiteSpace(record.source) ? "unknown" : record.source;
            string target = string.IsNullOrWhiteSpace(record.targetId) ? "-" : record.targetId;
            string action = string.IsNullOrWhiteSpace(record.actionType) ? "action" : record.actionType;

            if (string.Equals(action, "revert", StringComparison.Ordinal))
            {
                return $"[{timestamp}] {source} revert {target}";
            }

            return $"[{timestamp}] {source} {action} {target}";
        }

        private static string FormatTimestamp(string timestampUtc)
        {
            if (DateTimeOffset.TryParse(timestampUtc, out var parsed))
            {
                return parsed.ToLocalTime().ToString("HH:mm:ss");
            }

            return DateTimeOffset.Now.ToString("HH:mm:ss");
        }

        private void TrimRows()
        {
            if (actionContainer == null || maxVisibleRecords <= 0)
            {
                return;
            }

            while (actionContainer.childCount > maxVisibleRecords)
            {
                var child = actionContainer.GetChild(0);
                DestroyObject(child.gameObject);
            }
        }

        private void ClearRows()
        {
            if (actionContainer == null)
            {
                return;
            }

            for (int i = actionContainer.childCount - 1; i >= 0; i--)
            {
                DestroyObject(actionContainer.GetChild(i).gameObject);
            }
        }

        private static void DestroyObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void ScrollActionsToBottom()
        {
            if (actionScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            actionScrollRect.verticalNormalizedPosition = 0f;
        }

        private void ApplyViewState()
        {
            chatUiManager?.SetChatContentVisible(!showingActions);

            if (actionScrollRect != null)
            {
                actionScrollRect.gameObject.SetActive(showingActions);
            }

            if (manageTabButtonColors)
            {
                SetTabColor(chatTabButton, !showingActions);
                SetTabColor(actionsTabButton, showingActions);
            }
        }

        private static void SetTabColor(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = active ? GeneratedActiveTabColor : GeneratedInactiveTabColor;
            }
        }
    }
}
