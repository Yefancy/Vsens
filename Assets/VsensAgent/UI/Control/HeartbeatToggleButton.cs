using com.convalise.UnityMaterialSymbols;
using UnityEngine;
using UnityEngine.UI;
using VsensAgent.Core;
using VsensAgent.Network;

namespace VsensAgent.UI
{
    [DisallowMultipleComponent]
    public sealed class HeartbeatToggleButton : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private HeartbeatManager heartbeatManager;

        [Header("Icon Tint")]
        [SerializeField] private Graphic iconTintTarget;
        [SerializeField] private Color disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private Toggle heartbeatToggle;
        private Button heartbeatButton;
        private Color enabledColor = Color.white;
        private bool hasEnabledColor;
        private bool cachedState;

        private void Awake()
        {
            heartbeatToggle = GetComponent<Toggle>();
            heartbeatButton = GetComponent<Button>();
            ResolveDependencies();
            CacheEnabledColor();
        }

        private void OnEnable()
        {
            ResolveDependencies();

            if (heartbeatToggle == null && heartbeatButton == null)
            {
                Debug.LogWarning("[HeartbeatToggleButton] Missing Toggle/Button component.");
                return;
            }

            cachedState = heartbeatManager != null && heartbeatManager.IsHeartbeatEnabled;

            if (heartbeatToggle != null)
            {
                heartbeatToggle.SetIsOnWithoutNotify(cachedState);
                heartbeatToggle.onValueChanged.AddListener(OnToggleValueChanged);
            }

            if (heartbeatButton != null)
            {
                heartbeatButton.onClick.AddListener(OnButtonClicked);
            }

            ApplyVisualState(cachedState);
        }

        private void OnDisable()
        {
            if (heartbeatToggle != null)
            {
                heartbeatToggle.onValueChanged.RemoveListener(OnToggleValueChanged);
            }

            if (heartbeatButton != null)
            {
                heartbeatButton.onClick.RemoveListener(OnButtonClicked);
            }
        }

        public void RefreshFromManager()
        {
            ResolveDependencies();

            if (heartbeatToggle == null && heartbeatButton == null)
            {
                return;
            }

            cachedState = heartbeatManager != null && heartbeatManager.IsHeartbeatEnabled;
            if (heartbeatToggle != null)
            {
                heartbeatToggle.SetIsOnWithoutNotify(cachedState);
            }
            ApplyVisualState(cachedState);
        }

        private void OnToggleValueChanged(bool isOn)
        {
            ApplyHeartbeatState(isOn);
        }

        private void OnButtonClicked()
        {
            ApplyHeartbeatState(!cachedState);
        }

        private void ApplyHeartbeatState(bool isOn)
        {
            if (heartbeatManager == null)
            {
                ResolveDependencies();
            }

            if (heartbeatManager != null)
            {
                heartbeatManager.SetHeartbeatEnabled(isOn);
            }
            else
            {
                Debug.LogWarning("[HeartbeatToggleButton] HeartbeatManager not found. Toggle change cannot be applied.");
            }

            cachedState = isOn;
            if (heartbeatToggle != null)
            {
                heartbeatToggle.SetIsOnWithoutNotify(isOn);
            }
            ApplyVisualState(isOn);
        }

        private void ResolveDependencies()
        {
            if (heartbeatManager == null)
            {
                heartbeatManager = ServiceLocator.IsRegistered<HeartbeatManager>()
                    ? ServiceLocator.Get<HeartbeatManager>()
                    : FindFirstObjectByType<HeartbeatManager>();
            }

            if (iconTintTarget == null)
            {
                iconTintTarget = GetComponent<MaterialSymbol>();
                if (iconTintTarget == null)
                {
                    iconTintTarget = GetComponent<Graphic>();
                }
                if (iconTintTarget == null)
                {
                    iconTintTarget = GetComponentInChildren<MaterialSymbol>(true);
                }
            }

            CacheEnabledColor();
        }

        private void ApplyVisualState(bool isOn)
        {
            ApplyTintColor(isOn);
        }

        private void CacheEnabledColor()
        {
            if (hasEnabledColor || iconTintTarget == null)
            {
                return;
            }

            enabledColor = iconTintTarget.color;
            hasEnabledColor = true;
        }

        private void ApplyTintColor(bool isOn)
        {
            if (iconTintTarget == null)
            {
                return;
            }

            CacheEnabledColor();
            iconTintTarget.color = isOn ? enabledColor : disabledColor;
        }
    }
}