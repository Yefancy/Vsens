using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.Network.Protocol;
using VsensAgent.SceneApi.V2;
using VsensAgent.SceneHistory;
using VsensAgent.Tests.Editor.SceneApi;

namespace VsensAgent.Tests.Editor.SceneHistory
{
    public class SceneActionHistoryTests
    {
        [Test]
        public void RevertLast_RestoresTransformAndLogsRevert()
        {
            ServiceLocator.Clear();
            var root = new GameObject("SceneHistoryRoot");
            var target = new GameObject("MovableObject");

            try
            {
                var history = root.AddComponent<SceneActionHistory>();
                target.transform.position = new Vector3(1f, 2f, 3f);
                var before = SceneTransformSnapshot.Capture(target.name, target.transform);

                target.transform.position = new Vector3(4f, 5f, 6f);
                var after = SceneTransformSnapshot.Capture(target.name, target.transform);

                Assert.That(history.TryRecordTransformChange("user", "set_transform", target.name, before, after), Is.True);
                Assert.That(history.CanRevert, Is.True);
                Assert.That(history.OperationLog.Count, Is.EqualTo(1));
                Assert.That(history.OperationLog[0].timestampUtc, Is.Not.Empty);

                Assert.That(history.RevertLast("user", out var error), Is.True, error);

                Assert.That(target.transform.position.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(target.transform.position.y, Is.EqualTo(2f).Within(0.001f));
                Assert.That(target.transform.position.z, Is.EqualTo(3f).Within(0.001f));
                Assert.That(history.CanRevert, Is.False);
                Assert.That(history.OperationLog.Count, Is.EqualTo(2));
                Assert.That(history.OperationLog[1].actionType, Is.EqualTo("revert"));
                Assert.That(history.OperationLog[1].revertedActionId, Is.EqualTo(history.OperationLog[0].actionId));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void AgentSetAvatarTransform_RecordsStructuredActionAndCanRevert()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AgentSceneHistoryRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

            try
            {
                root.AddComponent<SceneRegistry>();
                root.AddComponent<SceneActionHistory>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TrySpawnAvatar("avatar_main", "smplx_male", Vector3.zero, Vector3.zero, out var spawnError), Is.True, spawnError);

                var controlManager = root.AddComponent<ControlManager>();
                var action = new ControlObject
                {
                    target = "avatar_main",
                    action = "set_avatar_transform",
                    parameters = new Dictionary<string, object>
                    {
                        ["position"] = new[] { 2f, 0f, 3f },
                        ["rotation"] = new[] { 0f, 45f, 0f }
                    }
                };

                Assert.That(controlManager.TryExecuteControlAction(action, out var errorCode, out var errorMessage), Is.True, $"{errorCode}: {errorMessage}");

                var history = SceneActionHistory.GetOrCreate();
                Assert.That(history.OperationLog.Count, Is.EqualTo(1));
                Assert.That(history.OperationLog[0].source, Is.EqualTo("agent"));
                Assert.That(history.OperationLog[0].actionType, Is.EqualTo("set_avatar_transform"));
                Assert.That(history.OperationLog[0].rawActionJson, Does.Contain("set_avatar_transform"));

                Assert.That(history.RevertLast("user", out var revertError), Is.True, revertError);
                var avatar = runtime.GetManagedAvatarObject();
                Assert.That(avatar.transform.position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(avatar.transform.position.z, Is.EqualTo(0f).Within(0.001f));
                Assert.That(history.OperationLog.Count, Is.EqualTo(2));
                Assert.That(history.OperationLog[1].actionType, Is.EqualTo("revert"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void SceneActionRecordNetworkLog_SerializesWithoutUnitySelfReferenceLoop()
        {
            var target = new GameObject("SerializableObject");

            try
            {
                target.transform.position = new Vector3(1f, 2f, 3f);
                target.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
                target.transform.localScale = new Vector3(2f, 2f, 2f);

                var record = new SceneActionRecord
                {
                    actionId = "action_001",
                    source = "user",
                    actionType = "set_transform",
                    targetId = target.name,
                    timestampUtc = "2026-04-21T10:11:12.123Z",
                    beforeTransform = SceneTransformSnapshot.Capture(target.name, target.transform),
                    afterTransform = SceneTransformSnapshot.Capture(target.name, target.transform)
                };

                var payload = new UnitySceneActionLogRequest
                {
                    scene_name = "Room Test",
                    record = record.ToNetworkLog()
                };

                var json = JsonConvert.SerializeObject(payload);
                Assert.That(json, Does.Contain("\"unity.scene_action\"").Or.Contain("\"type\":\"unity.scene_action\""));
                Assert.That(json, Does.Not.Contain("normalized"));
                Assert.That(json, Does.Contain("\"position\":{\"x\":1.0"));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
