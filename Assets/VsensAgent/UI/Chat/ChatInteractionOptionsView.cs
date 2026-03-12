using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VsensAgent.UI
{
    [Serializable]
    public class ChatInteractionOptionData
    {
        public string id;
        public string label;
        public string description;

        public ChatInteractionOptionData(string id, string label, string description = "")
        {
            this.id = id;
            this.label = label;
            this.description = description;
        }
    }

    public class ChatInteractionOptionsView : MonoBehaviour
    {
        private readonly List<OptionBinding> _optionBindings = new List<OptionBinding>();
        private readonly HashSet<string> _selectedOptionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Action<string[]> _onSubmit;
        private bool _allowMultiple;
        private TMP_FontAsset _fontAsset;
        private Button _submitButton;

        private static readonly Color NormalButtonColor = new Color(0.16f, 0.16f, 0.16f, 0.95f);
        private static readonly Color SelectedButtonColor = new Color(0.15f, 0.45f, 0.86f, 0.95f);
        private static readonly Color DisabledButtonColor = new Color(0.12f, 0.12f, 0.12f, 0.55f);

        public void Initialize(
            IReadOnlyList<ChatInteractionOptionData> options,
            bool allowMultiple,
            Action<string[]> onSubmit,
            TMP_FontAsset fontAsset = null)
        {
            ClearExistingChildren();
            _optionBindings.Clear();
            _selectedOptionIds.Clear();

            _allowMultiple = allowMultiple;
            _onSubmit = onSubmit;
            _fontAsset = fontAsset ?? TMP_Settings.defaultFontAsset;

            EnsureRootLayout();

            if (options != null)
            {
                foreach (var option in options)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.id))
                    {
                        continue;
                    }

                    _optionBindings.Add(CreateOptionButton(option));
                }
            }

            if (_allowMultiple)
            {
                _submitButton = CreateSubmitButton();
                UpdateSubmitButtonState();
            }
        }

        public void SetInteractable(bool interactable)
        {
            foreach (var binding in _optionBindings)
            {
                binding.button.interactable = interactable;
                binding.background.color = interactable
                    ? (_selectedOptionIds.Contains(binding.optionId) ? SelectedButtonColor : NormalButtonColor)
                    : DisabledButtonColor;
            }

            if (_submitButton != null)
            {
                _submitButton.interactable = interactable && _selectedOptionIds.Count > 0;
                var background = _submitButton.GetComponent<Image>();
                if (background != null)
                {
                    background.color = _submitButton.interactable ? SelectedButtonColor : DisabledButtonColor;
                }
            }
        }

        private void EnsureRootLayout()
        {
            var layoutGroup = GetComponent<VerticalLayoutGroup>();
            if (layoutGroup == null)
            {
                layoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layoutGroup.spacing = 6f;
            layoutGroup.childAlignment = TextAnchor.UpperLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            var fitter = GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private void ClearExistingChildren()
        {
            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                var child = transform.GetChild(index).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private OptionBinding CreateOptionButton(ChatInteractionOptionData option)
        {
            var buttonObject = new GameObject(
                $"Option_{option.id}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            buttonObject.transform.SetParent(transform, false);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 42f;
            layoutElement.flexibleWidth = 1f;

            var layoutGroup = buttonObject.GetComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(12, 12, 8, 8);
            layoutGroup.spacing = 0f;
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            var fitter = buttonObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var background = buttonObject.GetComponent<Image>();
            background.color = NormalButtonColor;
            background.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => OnOptionClicked(option.id));

            var label = CreateButtonLabel(buttonObject.transform, BuildOptionLabel(option));

            return new OptionBinding(option.id, button, background, label);
        }

        private Button CreateSubmitButton()
        {
            var buttonObject = new GameObject("SubmitSelectionButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(transform, false);

            var layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 38f;
            layoutElement.flexibleWidth = 1f;

            var background = buttonObject.GetComponent<Image>();
            background.color = DisabledButtonColor;
            background.raycastTarget = true;

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(SubmitMultiSelection);

            CreateButtonLabel(buttonObject.transform, "Submit selection");
            return button;
        }

        private TextMeshProUGUI CreateButtonLabel(Transform parent, string text)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            if (_fontAsset != null)
            {
                label.font = _fontAsset;
            }
            label.fontSize = 18f;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = text;

            return label;
        }

        private string BuildOptionLabel(ChatInteractionOptionData option)
        {
            if (string.IsNullOrWhiteSpace(option.description))
            {
                return option.label;
            }

            return $"{option.label}\n{option.description}";
        }

        private void OnOptionClicked(string optionId)
        {
            if (_allowMultiple)
            {
                if (_selectedOptionIds.Contains(optionId))
                {
                    _selectedOptionIds.Remove(optionId);
                }
                else
                {
                    _selectedOptionIds.Add(optionId);
                }

                UpdateSelectionVisuals();
                UpdateSubmitButtonState();
                return;
            }

            _onSubmit?.Invoke(new[] { optionId });
            SetInteractable(false);
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var binding in _optionBindings)
            {
                binding.background.color = _selectedOptionIds.Contains(binding.optionId)
                    ? SelectedButtonColor
                    : NormalButtonColor;
            }
        }

        private void UpdateSubmitButtonState()
        {
            if (_submitButton == null)
            {
                return;
            }

            _submitButton.interactable = _selectedOptionIds.Count > 0;
            var background = _submitButton.GetComponent<Image>();
            if (background != null)
            {
                background.color = _submitButton.interactable ? SelectedButtonColor : DisabledButtonColor;
            }
        }

        private void SubmitMultiSelection()
        {
            if (_selectedOptionIds.Count == 0)
            {
                return;
            }

            var selectedIds = new List<string>(_selectedOptionIds.Count);
            foreach (var binding in _optionBindings)
            {
                if (_selectedOptionIds.Contains(binding.optionId))
                {
                    selectedIds.Add(binding.optionId);
                }
            }

            _onSubmit?.Invoke(selectedIds.ToArray());
            SetInteractable(false);
        }

        private readonly struct OptionBinding
        {
            public readonly string optionId;
            public readonly Button button;
            public readonly Image background;
            public readonly TextMeshProUGUI label;

            public OptionBinding(string optionId, Button button, Image background, TextMeshProUGUI label)
            {
                this.optionId = optionId;
                this.button = button;
                this.background = background;
                this.label = label;
            }
        }
    }
}
