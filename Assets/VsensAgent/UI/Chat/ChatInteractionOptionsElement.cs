using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UIEButton = UnityEngine.UIElements.Button;

namespace VsensAgent.UI
{
    public sealed class ChatInteractionOptionsElement
    {
        private readonly List<UIEButton> _optionButtons = new();
        private readonly HashSet<string> _selectedIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool _allowMultiple;
        private readonly Action<string[]> _onSubmit;
        private readonly UIEButton _submitButton;

        public VisualElement Root { get; }

        public ChatInteractionOptionsElement(
            IReadOnlyList<ChatInteractionOptionData> options,
            bool allowMultiple,
            Action<string[]> onSubmit)
        {
            _allowMultiple = allowMultiple;
            _onSubmit = onSubmit;

            Root = new VisualElement { name = "chat-interaction-options" };
            Root.AddToClassList("chat-options");

            if (options != null)
            {
                foreach (var option in options)
                {
                    if (option == null || string.IsNullOrWhiteSpace(option.id))
                        continue;

                    var button = new UIEButton(() => OnOptionClicked(option.id))
                    {
                        name = $"chat-option-{option.id}"
                    };
                    button.AddToClassList("chat-option");
                    button.userData = (Action)(() => OnOptionClicked(option.id));
                    button.RegisterCallback<ClickEvent>(_ => OnOptionClicked(option.id));

                    var label = new Label(option.label ?? string.Empty);
                    label.AddToClassList("chat-option-label");
                    button.Add(label);

                    if (!string.IsNullOrWhiteSpace(option.description))
                    {
                        var description = new Label(option.description);
                        description.AddToClassList("chat-option-description");
                        button.Add(description);
                    }

                    _optionButtons.Add(button);
                    Root.Add(button);
                }
            }

            if (_allowMultiple)
            {
                _submitButton = new UIEButton(Submit)
                {
                    name = "chat-options-submit",
                    text = "Submit"
                };
                _submitButton.AddToClassList("chat-options-submit");
                _submitButton.userData = (Action)Submit;
                _submitButton.RegisterCallback<ClickEvent>(_ => Submit());
                _submitButton.SetEnabled(false);
                Root.Add(_submitButton);
            }
        }

        public void SetInteractable(bool interactable)
        {
            foreach (var button in _optionButtons)
                button?.SetEnabled(interactable);

            _submitButton?.SetEnabled(interactable && _selectedIds.Count > 0);
        }

        private void OnOptionClicked(string optionId)
        {
            if (_allowMultiple)
            {
                if (_selectedIds.Contains(optionId))
                    _selectedIds.Remove(optionId);
                else
                    _selectedIds.Add(optionId);

                foreach (var button in _optionButtons)
                {
                    var buttonOptionId = button.name.Replace("chat-option-", string.Empty);
                    button.EnableInClassList("chat-option--selected", _selectedIds.Contains(buttonOptionId));
                }

                if (_submitButton != null)
                    _submitButton.SetEnabled(_selectedIds.Count > 0);
                return;
            }

            _onSubmit?.Invoke(new[] { optionId });
            SetInteractable(false);
        }

        private void Submit()
        {
            if (_selectedIds.Count == 0)
                return;

            var ordered = new List<string>();
            foreach (var button in _optionButtons)
            {
                var optionId = button.name.Replace("chat-option-", string.Empty);
                if (_selectedIds.Contains(optionId))
                    ordered.Add(optionId);
            }

            _onSubmit?.Invoke(ordered.ToArray());
            SetInteractable(false);
        }
    }
}
