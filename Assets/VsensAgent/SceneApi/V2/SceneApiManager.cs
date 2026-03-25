using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.Network;
using VsensAgent;

namespace VsensAgent.SceneApi.V2
{
    public class SceneApiManager : MonoBehaviour
    {
        [SerializeField] private SceneRegistry sceneRegistry;
        [SerializeField] private ControlManager controlManager;
        [SerializeField] private AvatarRuntimeManager avatarRuntimeManager;

        private SceneQueryService _queryService;
        private SceneValidationService _validationService;
        private SceneTransactionExecutor _executor;

        private void Awake()
        {
            if (sceneRegistry == null)
            {
                sceneRegistry = GetComponent<SceneRegistry>();
                if (sceneRegistry == null)
                {
                    sceneRegistry = gameObject.AddComponent<SceneRegistry>();
                }
            }

            if (controlManager == null)
            {
                controlManager = ServiceLocator.Get<ControlManager>();
                if (controlManager == null)
                {
                    controlManager = FindFirstObjectByType<ControlManager>();
                }
            }

            if (controlManager == null)
            {
                Debug.LogError("[SceneApiManager] ❌ ControlManager not found. Scene API execute/validate is disabled.");
                enabled = false;
                return;
            }

            if (avatarRuntimeManager == null)
            {
                avatarRuntimeManager = GetComponent<AvatarRuntimeManager>();
                if (avatarRuntimeManager == null)
                {
                    avatarRuntimeManager = gameObject.AddComponent<AvatarRuntimeManager>();
                }
            }

            _queryService = new SceneQueryService(sceneRegistry, avatarRuntimeManager);
            _validationService = new SceneValidationService(sceneRegistry, avatarRuntimeManager);
            _executor = new SceneTransactionExecutor(sceneRegistry, controlManager);
        }

        private void OnEnable()
        {
            WsClient.OnSceneApiRequest += HandleSceneApiRequest;
        }

        private void OnDisable()
        {
            WsClient.OnSceneApiRequest -= HandleSceneApiRequest;
        }

        private void HandleSceneApiRequest(string rawJson)
        {
            string requestId = string.Empty;
            try
            {
                var node = JObject.Parse(rawJson);
                string type = node.Value<string>("type") ?? string.Empty;
                requestId = node.Value<string>("request_id") ?? string.Empty;

                object response = type switch
                {
                    "scene.query_summary" => _queryService.QuerySummary(node.Value<int?>("scene_version")),
                    "scene.query_objects" => _queryService.QueryObjects(
                        node["ids"]?.ToObject<List<string>>() ?? new List<string>(),
                        node["aliases"]?.ToObject<List<string>>() ?? new List<string>(),
                        node["tags"]?.ToObject<List<string>>() ?? new List<string>(),
                        node.Value<string>("zone_id") ?? string.Empty),
                    "scene.query_relations" => _queryService.QueryRelations(
                        node.Value<string>("object_id") ?? string.Empty,
                        node["relation_types"]?.ToObject<List<string>>() ?? new List<string>(),
                        node["radius"]?.ToObject<float?>()),
                    "scene.query_surfaces" => _queryService.QuerySurfaces(
                        node.Value<string>("near_object_id") ?? string.Empty,
                        node.Value<string>("zone_id") ?? string.Empty,
                        node.Value<bool?>("mountable_only") ?? true),
                    "scene.query_avatars" => _queryService.QueryAvatars(),
                    "scene.query_motions" => _queryService.QueryMotions(),
                    "scene.query_avatar_candidates" => _validationService.QueryAvatarCandidates(
                        node.Value<string>("target_object_id") ?? string.Empty,
                        node.Value<string>("target_alias") ?? string.Empty,
                        node.Value<string>("task_hint") ?? string.Empty,
                        node.Value<string>("preferred_side") ?? string.Empty,
                        node["preferred_distance"]?.ToObject<float?>(),
                        node["max_candidates"]?.ToObject<int?>()),
                    "scene.validate_avatar_placement" => _validationService.ValidateAvatarPlacement(
                        node.Value<string>("target_object_id") ?? string.Empty,
                        node.Value<string>("target_alias") ?? string.Empty,
                        node.Value<string>("task_hint") ?? string.Empty,
                        ReadVector3(node["position"]),
                        ReadVector3(node["rotation"]),
                        node.Value<string>("motion_json") ?? string.Empty,
                        node["trajectory_sample_count"]?.ToObject<int?>()),
                    "scene.capture_validation_views" => _validationService.CaptureValidationViews(
                        node.Value<string>("validator_context") ?? string.Empty,
                        node.Value<string>("avatar_id") ?? string.Empty,
                        node.Value<string>("target_object_id") ?? string.Empty,
                        node.Value<string>("target_alias") ?? string.Empty,
                        node["max_views"]?.ToObject<int?>()),
                    "scene.score_validation_views" => _validationService.ScoreValidationViews(
                        node.Value<string>("validator_context") ?? string.Empty,
                        node.Value<string>("avatar_id") ?? string.Empty,
                        node.Value<string>("target_object_id") ?? string.Empty,
                        node.Value<string>("target_alias") ?? string.Empty,
                        node.Value<string>("task_hint") ?? string.Empty,
                        node["max_views"]?.ToObject<int?>()),
                    "scene.find_sensor_placements" => HandleFindPlacements(node),
                    "scene.validate_placement" => HandleValidatePlacement(node),
                    "scene.validate_actions" => HandleValidateActions(node),
                    "scene.execute_actions" => HandleExecuteActions(node),
                    _ => new
                    {
                        type = "scene.error",
                        code = SceneApiErrorCodes.INVALID_PARAM,
                        message = $"Unsupported scene api request type '{type}'"
                    }
                };

                var responseObject = response as JObject ?? JObject.FromObject(response);
                WsClient.SendMessage(AddRequestId(responseObject, requestId));
            }
            catch (Exception ex)
            {
                var errorObject = JObject.FromObject(new
                {
                    type = "scene.error",
                    code = SceneApiErrorCodes.INVALID_PARAM,
                    message = ex.Message
                });
                WsClient.SendMessage(AddRequestId(errorObject, requestId));
            }
        }

        public static JObject AddRequestId(JObject response, string requestId)
        {
            if (response == null)
            {
                return new JObject();
            }

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                response["request_id"] = requestId;
            }

            return response;
        }

        private object HandleFindPlacements(JObject node)
        {
            var constraints = node["constraints"]?.ToObject<PlacementConstraints>() ?? new PlacementConstraints();
            return _queryService.FindSensorPlacements(
                node.Value<string>("sensor_type") ?? "DISTANCE",
                node["target_ids"]?.ToObject<List<string>>() ?? new List<string>(),
                constraints);
        }

        private object HandleValidatePlacement(JObject node)
        {
            var candidate = node["candidate"]?.ToObject<PlacementCandidate>();
            float minCoverage = node.Value<float?>("min_coverage") ?? 0.8f;
            return _queryService.ValidatePlacement(candidate, minCoverage);
        }

        private object HandleValidateActions(JObject node)
        {
            var request = ParseActionBatch(node);
            var report = _executor.Validate(request);
            return new
            {
                type = "scene.validate_actions_response",
                request_id = report.request_id,
                scene_version = report.scene_version,
                ok = report.ok,
                errors = report.errors,
                normalized_actions = report.normalized_actions
            };
        }

        private object HandleExecuteActions(JObject node)
        {
            var request = ParseActionBatch(node);
            var report = _executor.Execute(request);
            return new
            {
                type = "scene.execute_actions_response",
                request_id = report.request_id,
                status = report.status,
                scene_version_before = report.scene_version_before,
                scene_version_after = report.scene_version_after,
                failed_action_index = report.failed_action_index,
                applied_actions = report.applied_actions,
                rollback_status = report.rollback_status,
                duration_ms = report.duration_ms,
                errors = report.errors,
                trace_id = report.trace_id
            };
        }

        private static ActionBatchRequestV2 ParseActionBatch(JObject node)
        {
            var request = new ActionBatchRequestV2
            {
                request_id = node.Value<string>("request_id") ?? Guid.NewGuid().ToString("N"),
                scene_version = node.Value<int?>("scene_version") ?? -1,
                strict = node.Value<bool?>("strict") ?? true,
                actions = new List<ActionCommandV2>()
            };

            var actionsNode = node["actions"] as JArray;
            if (actionsNode == null)
            {
                return request;
            }

            foreach (var token in actionsNode)
            {
                var actionObj = token as JObject;
                if (actionObj == null) continue;
                var cmd = new ActionCommandV2
                {
                    target_id = actionObj.Value<string>("target_id") ?? string.Empty,
                    action_type = actionObj.Value<string>("action_type") ?? string.Empty,
                    parameters = actionObj["parameters"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
                    preconditions = actionObj["preconditions"]?.ToObject<List<string>>() ?? new List<string>()
                };
                request.actions.Add(cmd);
            }

            return request;
        }

        private static Vector3? ReadVector3(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token is JArray arr && arr.Count >= 3)
            {
                return new Vector3(
                    arr[0]!.Value<float>(),
                    arr[1]!.Value<float>(),
                    arr[2]!.Value<float>());
            }

            return null;
        }
    }
}
