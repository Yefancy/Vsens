using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using VsensAgent.Core;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;

namespace VsensAgent.RuntimeEditing
{
    public class ObjectSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera runtimeCamera;
        [SerializeField] private Color highlightColor = new Color(1f, 0.86f, 0.15f, 1f);

        private ObjectSelectionRequestMessage _activeRequest;
        private Action<ObjectSelectionReplyRequest> _replySender;
        private readonly HashSet<Renderer> _highlightedRenderers = new();

        public bool IsSelecting => _activeRequest != null;
        public GameObject HighlightedObject { get; private set; }

        private void Awake()
        {
            ServiceLocator.Register<ObjectSelectionController>(this);
            _replySender ??= WsClient.SendObjectSelectionReply;
        }

        private void OnEnable()
        {
            WsClient.OnObjectSelectionRequest -= BeginSelection;
            WsClient.OnObjectSelectionRequest += BeginSelection;
        }

        private void OnDisable()
        {
            WsClient.OnObjectSelectionRequest -= BeginSelection;
        }

        private void OnDestroy()
        {
            if (ServiceLocator.IsRegistered<ObjectSelectionController>() &&
                ServiceLocator.Get<ObjectSelectionController>() == this)
            {
                ServiceLocator.Unregister<ObjectSelectionController>();
            }
        }

        private void Update()
        {
            if (!IsSelecting)
            {
                return;
            }

            if (_activeRequest.allow_cancel && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelSelection();
                return;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            var camera = ResolveCamera();
            if (camera == null)
            {
                return;
            }

            TrySelectFromRay(camera.ScreenPointToRay(Input.mousePosition));
        }

        public void Configure(Camera camera, Action<ObjectSelectionReplyRequest> replySender)
        {
            runtimeCamera = camera;
            _replySender = replySender ?? WsClient.SendObjectSelectionReply;
        }

        public void BeginSelection(ObjectSelectionRequestMessage request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.selection_id))
            {
                return;
            }

            ClearHighlight();
            _activeRequest = request;
            _replySender ??= WsClient.SendObjectSelectionReply;
            Debug.Log($"[ObjectSelection] Waiting for selection: {request.prompt}");
        }

        public bool TrySelectFromRay(Ray ray)
        {
            if (!IsSelecting)
            {
                return false;
            }

            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(ray, 100f);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                var describer = ResolveDescriber(hit.collider != null ? hit.collider.gameObject : null);
                if (describer == null)
                {
                    continue;
                }

                var alias = describer.GetObjectName();
                if (!MatchesCandidate(alias))
                {
                    continue;
                }

                ApplyHighlight(describer.gameObject);
                SendReply(new ObjectSelectionReplyRequest
                {
                    selection_id = _activeRequest.selection_id,
                    selected_object_id = alias,
                    selected_alias = alias,
                    world_position = new[] { hit.point.x, hit.point.y, hit.point.z },
                    cancelled = false,
                });
                _activeRequest = null;
                return true;
            }

            foreach (var describer in FindObjectsByType<ObjectDescriber>(FindObjectsSortMode.None))
            {
                if (describer == null)
                {
                    continue;
                }

                var alias = describer.GetObjectName();
                if (!MatchesCandidate(alias) || !TryIntersectDescriberBounds(describer, ray, out var distance))
                {
                    continue;
                }

                ApplyHighlight(describer.gameObject);
                var point = ray.GetPoint(distance);
                SendReply(new ObjectSelectionReplyRequest
                {
                    selection_id = _activeRequest.selection_id,
                    selected_object_id = alias,
                    selected_alias = alias,
                    world_position = new[] { point.x, point.y, point.z },
                    cancelled = false,
                });
                _activeRequest = null;
                return true;
            }

            return false;
        }

        public bool CancelSelection()
        {
            if (!IsSelecting || !_activeRequest.allow_cancel)
            {
                return false;
            }

            SendReply(new ObjectSelectionReplyRequest
            {
                selection_id = _activeRequest.selection_id,
                cancelled = true,
                world_position = Array.Empty<float>(),
            });
            _activeRequest = null;
            ClearHighlight();
            return true;
        }

        private Camera ResolveCamera()
        {
            if (runtimeCamera != null)
            {
                return runtimeCamera;
            }

            runtimeCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            return runtimeCamera;
        }

        private static ObjectDescriber ResolveDescriber(GameObject hitObject)
        {
            return hitObject != null ? hitObject.GetComponentInParent<ObjectDescriber>() : null;
        }

        private static bool TryIntersectDescriberBounds(ObjectDescriber describer, Ray ray, out float distance)
        {
            distance = 0f;
            if (describer == null)
            {
                return false;
            }

            var collider = describer.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                return collider.bounds.IntersectRay(ray, out distance);
            }

            var renderer = describer.GetComponentInChildren<Renderer>();
            return renderer != null && renderer.bounds.IntersectRay(ray, out distance);
        }

        private bool MatchesCandidate(string alias)
        {
            var candidates = _activeRequest?.candidate_aliases;
            if (candidates == null || candidates.Length == 0)
            {
                return true;
            }

            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate, alias, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyHighlight(GameObject target)
        {
            ClearHighlight();
            HighlightedObject = target;
            if (target == null)
            {
                return;
            }

            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    continue;
                }

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", highlightColor);
                renderer.SetPropertyBlock(block);
                _highlightedRenderers.Add(renderer);
            }
        }

        private void ClearHighlight()
        {
            foreach (var renderer in _highlightedRenderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }

            _highlightedRenderers.Clear();
            HighlightedObject = null;
        }

        private void SendReply(ObjectSelectionReplyRequest reply)
        {
            reply.type = "object_selection.reply";
            (_replySender ?? WsClient.SendObjectSelectionReply).Invoke(reply);
        }
    }
}
