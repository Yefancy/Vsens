using System;
using TMPro;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;
using VsensAgent.SceneHistory;

namespace VsensAgent.UI
{
    public sealed class UniversalStatusBar : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private string defaultAgentStatus = "Agent: Idle";
        [SerializeField] private Color hoverHintColor = new Color(0.78f, 0.56f, 0.12f, 1f);

        private string agentStatus;
        private string hoverHint;
        private string transientMessage;
        private float transientUntil;
        private SceneActionHistory sceneActionHistory;
        private Color defaultTextColor;
        private bool hasDefaultTextColor;

        private void Awake()
        {
            statusText ??= GetComponentInChildren<TMP_Text>(true);

            if (statusText != null)
            {
                defaultTextColor = statusText.color;
                hasDefaultTextColor = true;
            }

            agentStatus = defaultAgentStatus;
            ServiceLocator.Register<UniversalStatusBar>(this);
            UpdateDisplayedText();
        }

        private void OnEnable()
        {
            WsClient.OnAgentStatus += OnAgentStatus;
            SubscribeSceneHistory();
            UpdateDisplayedText();
        }

        private void OnDisable()
        {
            WsClient.OnAgentStatus -= OnAgentStatus;
            if (sceneActionHistory != null)
            {
                sceneActionHistory.OperationRecorded -= OnOperationRecorded;
            }
        }

        private void OnDestroy()
        {
            if (ServiceLocator.IsRegistered<UniversalStatusBar>() && ServiceLocator.Get<UniversalStatusBar>() == this)
            {
                ServiceLocator.Unregister<UniversalStatusBar>();
            }
        }

        private void Update()
        {
            if (sceneActionHistory == null)
            {
                SubscribeSceneHistory();
            }

            if (!string.IsNullOrEmpty(transientMessage) && Time.unscaledTime >= transientUntil)
            {
                transientMessage = null;
                UpdateDisplayedText();
            }
        }

        public void SetAgentStatus(string state, string detail = null)
        {
            string displayState = string.IsNullOrWhiteSpace(state) ? "Idle" : ToTitleCase(state);
            agentStatus = string.IsNullOrWhiteSpace(detail)
                ? $"Agent: {displayState}"
                : $"Agent: {displayState} - {detail}";
            UpdateDisplayedText();
        }

        public void PushMessage(string message, float durationSeconds = 2f)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            transientMessage = message;
            transientUntil = Time.unscaledTime + Mathf.Max(0.1f, durationSeconds);
            UpdateDisplayedText();
        }

        public void SetHoverHint(string hint)
        {
            hoverHint = string.IsNullOrWhiteSpace(hint) ? null : hint;
            UpdateDisplayedText();
        }

        public void ClearHoverHint(string hint = null)
        {
            if (!string.IsNullOrEmpty(hint) && !string.Equals(hoverHint, hint, StringComparison.Ordinal))
            {
                return;
            }

            hoverHint = null;
            UpdateDisplayedText();
        }

        private void OnAgentStatus(AgentStatusMessage message)
        {
            if (message == null)
            {
                return;
            }

            SetAgentStatus(message.state, message.detail);
        }

        private void SubscribeSceneHistory()
        {
            sceneActionHistory ??= ServiceLocator.IsRegistered<SceneActionHistory>()
                ? ServiceLocator.Get<SceneActionHistory>()
                : FindFirstObjectByType<SceneActionHistory>();

            if (sceneActionHistory == null)
            {
                return;
            }

            sceneActionHistory.OperationRecorded -= OnOperationRecorded;
            sceneActionHistory.OperationRecorded += OnOperationRecorded;
        }

        private void OnOperationRecorded(SceneActionRecord record)
        {
            if (record == null)
            {
                return;
            }

            if (string.Equals(record.actionType, "revert", StringComparison.Ordinal))
            {
                PushMessage($"Reverted {record.targetId}", 2f);
            }
        }

        private void UpdateDisplayedText()
        {
            if (statusText == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(hoverHint))
            {
                statusText.color = hoverHintColor;
                statusText.text = hoverHint;
                return;
            }

            if (hasDefaultTextColor)
            {
                statusText.color = defaultTextColor;
            }

            if (!string.IsNullOrEmpty(transientMessage))
            {
                statusText.text = transientMessage;
                return;
            }

            statusText.text = string.IsNullOrEmpty(agentStatus) ? defaultAgentStatus : agentStatus;
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            return char.ToUpperInvariant(value[0]) + (value.Length > 1 ? value[1..] : string.Empty);
        }
    }
}
