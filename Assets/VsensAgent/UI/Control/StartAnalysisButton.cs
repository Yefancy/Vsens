using com.convalise.UnityMaterialSymbols;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;

namespace VsensAgent.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class StartAnalysisButton : MonoBehaviour
    {
        private enum AnalysisSelector
        {
            Latest,
            All,
            RecentN,
            ByLabel,
        }

        [Header("Request")]
        [SerializeField] private AnalysisSelector selector = AnalysisSelector.Latest;
        [SerializeField] private int recentN = 3;
        [SerializeField] private string timestampLabel = string.Empty;
        [SerializeField] private string analysisGoal = "Analyze the selected recordings and summarize which placement works best.";

        [Header("UI")]
        [SerializeField] private Graphic tintTarget;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string idleLabel = "Start Analysis";
        [SerializeField] private string pendingLabel = "Analyzing...";
        [SerializeField] private Color pendingTint = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private float startTimeoutSeconds = 5f;

        private Button _button;
        private Color _idleTint = Color.white;
        private bool _hasIdleTint;
        private bool _isPending;
        private bool _awaitingStart;
        private float _awaitingStartUntil;

        private void Awake()
        {
            ResolveComponents();
            CacheIdleTint();
        }

        private void OnEnable()
        {
            ResolveComponents();
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
                _button.onClick.AddListener(OnClick);
            }

            WsClient.OnJobLifecycle -= OnJobLifecycle;
            WsClient.OnJobLifecycle += OnJobLifecycle;
            WsClient.OnDelegatedTaskResult -= OnDelegatedTaskResult;
            WsClient.OnDelegatedTaskResult += OnDelegatedTaskResult;

            ApplyIdleVisualState();
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }

            WsClient.OnJobLifecycle -= OnJobLifecycle;
            WsClient.OnDelegatedTaskResult -= OnDelegatedTaskResult;
        }

        private void Update()
        {
            if (_awaitingStart && Time.unscaledTime >= _awaitingStartUntil)
            {
                ResetToIdle();
            }
        }

        public void RequestAnalysis()
        {
            if (_isPending || !WsClient.IsConnected)
            {
                return;
            }

            switch (selector)
            {
                case AnalysisSelector.All:
                    WsClient.SendAnalysisTaskAll(analysisGoal);
                    break;
                case AnalysisSelector.RecentN:
                    WsClient.SendAnalysisTaskRecent(recentN, analysisGoal);
                    break;
                case AnalysisSelector.ByLabel:
                    WsClient.SendAnalysisTaskByLabel(timestampLabel, analysisGoal);
                    break;
                default:
                    WsClient.SendAnalysisTaskLatest(analysisGoal);
                    break;
            }

            ApplyPendingVisualState();
            _awaitingStart = true;
            _awaitingStartUntil = Time.unscaledTime + Mathf.Max(0.5f, startTimeoutSeconds);
        }

        public void ResetToIdle()
        {
            _awaitingStart = false;
            _awaitingStartUntil = 0f;
            ApplyIdleVisualState();
        }

        public void SetTimestampLabel(string newTimestampLabel)
        {
            timestampLabel = newTimestampLabel ?? string.Empty;
        }

        public void SetRecentN(int value)
        {
            recentN = Mathf.Max(1, value);
        }

        public void SetAnalysisGoal(string value)
        {
            analysisGoal = value ?? string.Empty;
        }

        private void OnClick()
        {
            RequestAnalysis();
        }

        private void OnJobLifecycle(JobLifecycleMessage job)
        {
            if (job == null || job.job_kind != "delegated_task")
            {
                return;
            }

            switch (job.type)
            {
                case "job.started":
                    _awaitingStart = false;
                    _isPending = true;
                    ApplyPendingVisualState();
                    break;
                case "job.completed":
                case "job.cancelled":
                    ResetToIdle();
                    break;
                case "job.status":
                    if (job.status == "failed")
                    {
                        ResetToIdle();
                    }
                    break;
            }
        }

        private void OnDelegatedTaskResult(DelegatedTaskResultMessage result)
        {
            if (result == null)
            {
                return;
            }

            ResetToIdle();
        }

        private void ApplyPendingVisualState()
        {
            _isPending = true;
            if (_button != null)
            {
                _button.interactable = false;
            }

            if (label != null && !string.IsNullOrWhiteSpace(pendingLabel))
            {
                label.text = pendingLabel;
            }

            if (tintTarget != null)
            {
                CacheIdleTint();
                tintTarget.color = pendingTint;
            }
        }

        private void ApplyIdleVisualState()
        {
            _isPending = false;
            if (_button != null)
            {
                _button.interactable = true;
            }

            if (label != null && !string.IsNullOrWhiteSpace(idleLabel))
            {
                label.text = idleLabel;
            }

            if (tintTarget != null)
            {
                CacheIdleTint();
                tintTarget.color = _idleTint;
            }
        }

        private void CacheIdleTint()
        {
            if (_hasIdleTint || tintTarget == null)
            {
                return;
            }

            _idleTint = tintTarget.color;
            _hasIdleTint = true;
        }

        private void ResolveComponents()
        {
            _button ??= GetComponent<Button>();

            if (tintTarget == null)
            {
                tintTarget = GetComponent<MaterialSymbol>();
            }
            if (tintTarget == null)
            {
                tintTarget = GetComponent<Graphic>();
            }
            if (tintTarget == null && _button != null)
            {
                tintTarget = _button.targetGraphic;
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }
        }
    }
}
