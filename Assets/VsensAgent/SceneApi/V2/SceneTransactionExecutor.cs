using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using VsensAgent.Network.Protocol;
using VsensAgent;

namespace VsensAgent.SceneApi.V2
{
    public class SceneTransactionExecutor
    {
        private readonly SceneRegistry _registry;
        private readonly ControlManager _controlManager;

        public SceneTransactionExecutor(SceneRegistry registry, ControlManager controlManager)
        {
            _registry = registry;
            _controlManager = controlManager;
        }

        public ValidationReportV2 Validate(ActionBatchRequestV2 request)
        {
            var report = new ValidationReportV2
            {
                request_id = request?.request_id,
                scene_version = _registry.CurrentVersion,
                ok = true
            };

            if (request == null)
            {
                report.ok = false;
                report.errors.Add(new ActionErrorItemV2
                {
                    code = SceneApiErrorCodes.INVALID_PARAM,
                    message = "Request is null",
                    action_index = -1
                });
                return report;
            }

            if (request.scene_version != _registry.CurrentVersion)
            {
                report.ok = false;
                report.errors.Add(new ActionErrorItemV2
                {
                    code = SceneApiErrorCodes.SCENE_VERSION_MISMATCH,
                    message = $"request={request.scene_version}, current={_registry.CurrentVersion}",
                    action_index = -1,
                    hint = "Refresh scene summary and retry"
                });
                return report;
            }

            if (request.actions == null || request.actions.Count == 0)
            {
                report.ok = false;
                report.errors.Add(new ActionErrorItemV2
                {
                    code = SceneApiErrorCodes.INVALID_PARAM,
                    message = "Action batch is empty",
                    action_index = -1
                });
                return report;
            }

            for (int i = 0; i < request.actions.Count; i++)
            {
                var cmd = request.actions[i];
                if (!TryToLegacyControl(cmd, out var control, out var conversionError))
                {
                    report.ok = false;
                    report.errors.Add(new ActionErrorItemV2
                    {
                        code = SceneApiErrorCodes.INVALID_PARAM,
                        message = conversionError,
                        action_index = i,
                        target_id = cmd?.target_id
                    });
                    continue;
                }

                if (!_controlManager.TryValidateControlAction(control, out var errCode, out var errMsg))
                {
                    report.ok = false;
                    report.errors.Add(new ActionErrorItemV2
                    {
                        code = errCode,
                        message = errMsg,
                        action_index = i,
                        target_id = cmd.target_id
                    });
                    continue;
                }

                report.normalized_actions.Add(cmd);
            }

            return report;
        }

        public ExecutionReportV2 Execute(ActionBatchRequestV2 request)
        {
            var sw = Stopwatch.StartNew();
            var report = new ExecutionReportV2
            {
                request_id = request?.request_id,
                scene_version_before = _registry.CurrentVersion,
                scene_version_after = _registry.CurrentVersion,
                status = "failed",
                rollback_status = "not_needed",
                trace_id = Guid.NewGuid().ToString("N")
            };

            var validation = Validate(request);
            if (!validation.ok)
            {
                report.errors = validation.errors;
                report.duration_ms = sw.ElapsedMilliseconds;
                return report;
            }

            var snapshots = new List<RollbackSnapshot>();
            bool strict = request.strict;
            _registry.BeginMutationBatch("scene.execute_actions");

            for (int i = 0; i < request.actions.Count; i++)
            {
                var cmd = request.actions[i];
                TryToLegacyControl(cmd, out var control, out _);

                var targetObj = ResolveTargetObject(cmd.target_id);
                if (targetObj != null)
                {
                    snapshots.Add(RollbackSnapshot.From(targetObj));
                }

                if (!_controlManager.TryExecuteControlAction(control, out var errCode, out var errMsg))
                {
                    report.failed_action_index = i;
                    report.errors.Add(new ActionErrorItemV2
                    {
                        code = errCode,
                        message = errMsg,
                        target_id = cmd.target_id,
                        action_index = i
                    });

                    if (strict)
                    {
                        var rollbackOk = Rollback(snapshots);
                        report.rollback_status = rollbackOk ? "success" : "partial_failure";
                        if (!rollbackOk)
                        {
                            report.errors.Add(new ActionErrorItemV2
                            {
                                code = SceneApiErrorCodes.TRANSACTION_ROLLBACK_FAILED,
                                message = "Rollback did not complete for all snapshots",
                                action_index = i
                            });
                            _registry.CommitMutationBatch();
                        }
                        else
                        {
                            _registry.RollbackMutationBatch();
                        }
                    }
                    else
                    {
                        _registry.CommitMutationBatch();
                    }

                    report.duration_ms = sw.ElapsedMilliseconds;
                    report.scene_version_after = _registry.CurrentVersion;
                    return report;
                }

                report.applied_actions.Add(i);
            }

            _registry.CommitMutationBatch();
            report.scene_version_after = _registry.CurrentVersion;
            report.status = "success";
            report.failed_action_index = -1;
            report.rollback_status = "not_needed";
            report.duration_ms = sw.ElapsedMilliseconds;
            return report;
        }

        private bool TryToLegacyControl(ActionCommandV2 cmd, out ControlObject control, out string error)
        {
            control = null;
            error = null;

            if (cmd == null)
            {
                error = "Action command is null";
                return false;
            }

            if (string.IsNullOrWhiteSpace(cmd.action_type))
            {
                error = "action_type is required";
                return false;
            }

            var targetName = ResolveTargetName(cmd.target_id);
            if (string.IsNullOrWhiteSpace(targetName) && IsAvatarAction(cmd.action_type))
            {
                targetName = ResolveAvatarTargetName(cmd.parameters);
            }

            if (string.IsNullOrWhiteSpace(targetName) && cmd.action_type != "set_sensor" && !IsAvatarAction(cmd.action_type))
            {
                error = $"Cannot resolve target_id '{cmd.target_id}'";
                return false;
            }

            var parameters = cmd.parameters ?? new Dictionary<string, object>();

            control = new ControlObject
            {
                target = targetName,
                action = cmd.action_type,
                parameters = parameters
            };

            return true;
        }

        private static bool IsAvatarAction(string actionType)
        {
            switch (actionType)
            {
                case "spawn_avatar":
                case "remove_avatar":
                case "set_avatar_transform":
                case "load_avatar_motion":
                case "play_avatar_motion":
                case "pause_avatar_motion":
                case "stop_avatar_motion":
                case "clear_avatar_motion":
                    return true;
                default:
                    return false;
            }
        }

        private static string ResolveAvatarTargetName(Dictionary<string, object> parameters)
        {
            if (parameters != null && parameters.TryGetValue("avatar_id", out var avatarId) && avatarId != null)
            {
                return avatarId.ToString();
            }

            return "avatar_main";
        }

        private string ResolveTargetName(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return string.Empty;
            if (!_registry.TryResolveObject(targetId, out var describer) || describer == null)
            {
                return string.Empty;
            }
            return describer.GetObjectName();
        }

        private GameObject ResolveTargetObject(string targetId)
        {
            if (!_registry.TryResolveObject(targetId, out var describer) || describer == null)
            {
                return null;
            }
            return describer.gameObject;
        }

        private static bool Rollback(List<RollbackSnapshot> snapshots)
        {
            bool ok = true;
            for (int i = snapshots.Count - 1; i >= 0; i--)
            {
                ok &= snapshots[i].Restore();
            }
            return ok;
        }

        private sealed class RollbackSnapshot
        {
            private readonly GameObject _target;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _localScale;
            private readonly string _state;
            private readonly bool _hasState;

            private RollbackSnapshot(GameObject target)
            {
                _target = target;
                _position = target.transform.position;
                _rotation = target.transform.rotation;
                _localScale = target.transform.localScale;
                if (target.TryGetComponent<IStateHolder>(out var stateHolder))
                {
                    _hasState = true;
                    _state = stateHolder.getCurrentState();
                }
            }

            public static RollbackSnapshot From(GameObject target)
            {
                return new RollbackSnapshot(target);
            }

            public bool Restore()
            {
                if (_target == null) return false;
                _target.transform.position = _position;
                _target.transform.rotation = _rotation;
                _target.transform.localScale = _localScale;

                if (_hasState && _target.TryGetComponent<IStateHolder>(out var stateHolder))
                {
                    stateHolder.setCurrentState(_state);
                }

                return true;
            }
        }
    }
}
