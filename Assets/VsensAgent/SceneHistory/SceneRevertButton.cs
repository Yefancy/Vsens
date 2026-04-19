using UnityEngine;
using UnityEngine.UI;

namespace VsensAgent.SceneHistory
{
    [RequireComponent(typeof(Button))]
    public sealed class SceneRevertButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private SceneActionHistory history;

        private void Awake()
        {
            button ??= GetComponent<Button>();
            history ??= SceneActionHistory.GetOrCreate();
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }

            if (history != null)
            {
                history.RevertAvailabilityChanged -= OnRevertAvailabilityChanged;
                history.RevertAvailabilityChanged += OnRevertAvailabilityChanged;
                OnRevertAvailabilityChanged(history.CanRevert, history.LastRevertableAction);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }

            if (history != null)
            {
                history.RevertAvailabilityChanged -= OnRevertAvailabilityChanged;
            }
        }

        private void OnClick()
        {
            history ??= SceneActionHistory.GetOrCreate();
            if (history == null)
            {
                return;
            }

            if (!history.RevertLast("user", out var error) && !string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning($"[SceneRevertButton] Revert failed: {error}");
            }
        }

        private void OnRevertAvailabilityChanged(bool canRevert, SceneActionRecord _)
        {
            if (button != null)
            {
                button.interactable = canRevert;
            }
        }
    }
}
