using System;
using System.Collections.Generic;
using Sensor;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.Network;
using VsensAgent.RuntimeEditing;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.SceneHistory
{
    public sealed class SceneActionHistory : MonoBehaviour
    {
        private readonly List<SceneActionRecord> operationLog = new();
        private readonly Stack<SceneActionRecord> undoStack = new();
        private bool isReverting;

        public event Action<SceneActionRecord> OperationRecorded;
        public event Action<bool, SceneActionRecord> RevertAvailabilityChanged;

        public bool CanRevert => undoStack.Count > 0;
        public SceneActionRecord LastRevertableAction => undoStack.Count > 0 ? undoStack.Peek() : null;
        public IReadOnlyList<SceneActionRecord> OperationLog => operationLog;

        private void Awake()
        {
            if (ServiceLocator.IsRegistered<SceneActionHistory>() &&
                ServiceLocator.Get<SceneActionHistory>() != this)
            {
                Debug.LogWarning("[SceneActionHistory] Duplicate history service detected; keeping the existing instance.");
                return;
            }

            ServiceLocator.Register<SceneActionHistory>(this);
            NotifyAvailabilityChanged();
        }

        private void OnDestroy()
        {
            if (ServiceLocator.IsRegistered<SceneActionHistory>() &&
                ServiceLocator.Get<SceneActionHistory>() == this)
            {
                ServiceLocator.Unregister<SceneActionHistory>();
            }
        }

        public static SceneActionHistory GetOrCreate()
        {
            if (ServiceLocator.IsRegistered<SceneActionHistory>())
            {
                return ServiceLocator.Get<SceneActionHistory>();
            }

            var existing = FindFirstObjectByType<SceneActionHistory>();
            if (existing != null)
            {
                ServiceLocator.Register<SceneActionHistory>(existing);
                return existing;
            }

            var host = new GameObject("SceneActionHistory");
            return host.AddComponent<SceneActionHistory>();
        }

        public bool TryRecordTransformChange(
            string source,
            string actionType,
            string targetId,
            SceneTransformSnapshot before,
            SceneTransformSnapshot after,
            string rawActionJson = null,
            string description = null)
        {
            if (isReverting || before == null || after == null || before.ApproximatelyEquals(after))
            {
                return false;
            }

            var record = new SceneActionRecord
            {
                actionId = Guid.NewGuid().ToString("N"),
                source = string.IsNullOrWhiteSpace(source) ? "unknown" : source,
                actionType = string.IsNullOrWhiteSpace(actionType) ? "set_transform" : actionType,
                targetId = string.IsNullOrWhiteSpace(targetId) ? after.objectId : targetId,
                timestampUtc = DateTimeOffset.UtcNow.ToString("o"),
                description = description,
                rawActionJson = rawActionJson,
                beforeTransform = before,
                afterTransform = after
            };

            operationLog.Add(record);
            undoStack.Push(record);
            OperationRecorded?.Invoke(record);
            NotifyAvailabilityChanged();
            WsClient.SendSceneActionLog(record);
            return true;
        }

        public bool RevertLast(out string error)
        {
            return RevertLast("user", out error);
        }

        public bool RevertLast(string source, out string error)
        {
            error = null;
            if (undoStack.Count == 0)
            {
                error = "No scene action to revert.";
                NotifyAvailabilityChanged();
                return false;
            }

            var record = undoStack.Pop();
            isReverting = true;
            try
            {
                if (!TryApplyTransformSnapshot(record.targetId, record.beforeTransform, out error))
                {
                    undoStack.Push(record);
                    NotifyAvailabilityChanged();
                    return false;
                }
            }
            finally
            {
                isReverting = false;
            }

            var revertRecord = new SceneActionRecord
            {
                actionId = Guid.NewGuid().ToString("N"),
                source = string.IsNullOrWhiteSpace(source) ? "user" : source,
                actionType = "revert",
                targetId = record.targetId,
                timestampUtc = DateTimeOffset.UtcNow.ToString("o"),
                description = $"Reverted {record.actionType} on {record.targetId}",
                revertedActionId = record.actionId,
                beforeTransform = record.afterTransform,
                afterTransform = record.beforeTransform
            };

            operationLog.Add(revertRecord);
            OperationRecorded?.Invoke(revertRecord);
            NotifyAvailabilityChanged();
            WsClient.SendSceneActionLog(revertRecord);
            return true;
        }

        public void Clear()
        {
            operationLog.Clear();
            undoStack.Clear();
            NotifyAvailabilityChanged();
        }

        private bool TryApplyTransformSnapshot(string targetId, SceneTransformSnapshot snapshot, out string error)
        {
            error = null;
            if (snapshot == null)
            {
                error = "Snapshot is missing.";
                return false;
            }

            var effectiveTargetId = string.IsNullOrWhiteSpace(targetId) ? snapshot.objectId : targetId;
            var editableController = ResolveEditController();
            editableController?.ClearSelectionIfSelected(effectiveTargetId);

            var avatarRuntime = ResolveAvatarRuntimeManager();
            if (avatarRuntime != null && avatarRuntime.GetManagedAvatarObject() != null)
            {
                var avatar = avatarRuntime.GetManagedAvatarObject();
                if (string.Equals(avatar.name, effectiveTargetId, StringComparison.Ordinal))
                {
                    var euler = AvatarRuntimeManager.GetLogicalRotationEuler(snapshot.rotation);
                    if (!avatarRuntime.TrySetAvatarTransform(effectiveTargetId, snapshot.position, euler, snapshot.localScale, out error))
                    {
                        return false;
                    }

                    RegisterMutation("history.revert", effectiveTargetId, "revert");
                    return true;
                }
            }

            var target = GameObject.Find(effectiveTargetId);
            if (target == null)
            {
                error = $"Target '{effectiveTargetId}' not found.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.parentName))
            {
                var parent = GameObject.Find(snapshot.parentName);
                target.transform.SetParent(parent != null ? parent.transform : null, true);
            }
            else
            {
                target.transform.SetParent(null, true);
            }

            target.transform.position = snapshot.position;
            target.transform.rotation = snapshot.rotation;
            target.transform.localScale = snapshot.localScale;

            if (target.TryGetComponent<VirtualSensor>(out _))
            {
                RegisterMutation("history.revert", effectiveTargetId, "set_sensor");
            }
            else
            {
                RegisterMutation("history.revert", effectiveTargetId, "set_transform");
            }

            return true;
        }

        private RuntimeEditModeController ResolveEditController()
        {
            return ServiceLocator.IsRegistered<RuntimeEditModeController>()
                ? ServiceLocator.Get<RuntimeEditModeController>()
                : FindFirstObjectByType<RuntimeEditModeController>();
        }

        private AvatarRuntimeManager ResolveAvatarRuntimeManager()
        {
            return ServiceLocator.IsRegistered<AvatarRuntimeManager>()
                ? ServiceLocator.Get<AvatarRuntimeManager>()
                : FindFirstObjectByType<AvatarRuntimeManager>();
        }

        private void RegisterMutation(string source, string targetId, string actionType)
        {
            var registry = ServiceLocator.IsRegistered<SceneRegistry>()
                ? ServiceLocator.Get<SceneRegistry>()
                : FindFirstObjectByType<SceneRegistry>();
            registry?.RegisterMutation(source, targetId, actionType);
        }

        private void NotifyAvailabilityChanged()
        {
            RevertAvailabilityChanged?.Invoke(CanRevert, LastRevertableAction);
        }
    }
}
