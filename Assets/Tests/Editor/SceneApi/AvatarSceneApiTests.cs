using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Animations;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using VsensAgent;
using VsensAgent.Core;
using VsensAgent.Network.Protocol;
using VsensAgent.SceneApi.V2;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VsensAgent.Tests.Editor.SceneApi
{
    public class AvatarSceneApiTests
    {
        private static JObject ToObject(object value) => JObject.FromObject(value);

        [Test]
        public void QueryAvatars_ReturnsSpawnedAvatarInSceneModel()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSceneApiRoot");
            var prefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);

                string error;
                var spawned = runtime.TrySpawnAvatar(
                    "avatar_main",
                    "smplx_male",
                    new Vector3(1f, 0f, 2f),
                    new Vector3(0f, 90f, 0f),
                    out error
                );

                Assert.That(spawned, Is.True, error);

                var queryService = new SceneQueryService(registry, runtime);
                var response = ToObject(queryService.QueryAvatars());
                var avatars = response["avatars"]!.ToObject<List<AvatarQueryModel>>();

                Assert.That(response.Value<string>("method"), Is.EqualTo("scene.query_avatars"));
                Assert.That(avatars.Count, Is.EqualTo(1));
                Assert.That(avatars[0].avatar_id, Is.EqualTo("avatar_main"));
                Assert.That(avatars[0].pose_authority, Is.EqualTo("agent"));

                var snapshot = registry.BuildSnapshot(includeRelations: false);
                Assert.That(snapshot.objects.Exists(o => o.alias == "avatar_main"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void QueryAvatarAttachmentPoints_ReturnsSemanticBodyPointsForManagedAvatar()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarAttachmentPointRoot");
            var prefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);

                Assert.That(runtime.TrySpawnAvatar(
                    "avatar_main",
                    "smplx_male",
                    Vector3.zero,
                    Vector3.zero,
                    out var spawnError), Is.True, spawnError);

                var queryService = new SceneQueryService(registry, runtime);
                var response = ToObject(queryService.QueryAvatarAttachmentPoints("avatar_main"));
                var points = response["attachment_points"]!.ToObject<List<AvatarAttachmentPointQueryModel>>();

                Assert.That(response.Value<string>("method"), Is.EqualTo("scene.query_avatar_attachment_points"));
                Assert.That(points, Is.Not.Null);
                Assert.That(points.Count, Is.GreaterThanOrEqualTo(7));
                Assert.That(points.Exists(p => p.joint_name == "left_wrist"), Is.True);
                Assert.That(points.Exists(p => p.joint_name == "right_wrist"), Is.True);
                Assert.That(points.Exists(p => p.joint_name == "head"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void SetSensor_WithAvatarJointAttachment_ParentsSensorToResolvedJoint()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSensorAttachRoot");
            var prefab = AvatarSceneApiTestHelpers.CreateAttachmentAwareAvatarPrefab();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TrySpawnAvatar("avatar_main", "smplx_male", Vector3.zero, Vector3.zero, out var spawnError), Is.True, spawnError);

                var sensorObject = new GameObject("imu_left_wrist");
                var attachedSensor = sensorObject.AddComponent<TestVirtualSensor>();
                sensorObject.AddComponent<SensorObjectDescriber>();
                sensorObject.name = "imu_left_wrist";
                sensorObject.transform.position = new Vector3(2f, 2f, 2f);

                var controlManager = root.AddComponent<ControlManager>();
                var ctrl = new ControlObject
                {
                    target = "imu_left_wrist",
                    action = "set_sensor",
                    parameters = new Dictionary<string, object>
                    {
                        ["sensor_type"] = "IMU",
                        ["attach_mode"] = "avatar_joint",
                        ["avatar_id"] = "avatar_main",
                        ["joint_name"] = "left_wrist",
                    }
                };

                Assert.That(controlManager.TryExecuteControlAction(ctrl, out var errorCode, out var errorMessage), Is.True, $"{errorCode}: {errorMessage}");

                Assert.That(attachedSensor, Is.Not.Null);
                Assert.That(attachedSensor.transform.parent, Is.Not.Null);
                Assert.That(attachedSensor.transform.parent.name.ToLowerInvariant(), Does.Contain("wrist"));
                Assert.That(attachedSensor.transform.root.name, Is.EqualTo("avatar_main"));
                Object.DestroyImmediate(sensorObject);
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void SpawnAvatar_ReducesRequestedHeightTowardFloor()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSnapRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);

                Assert.That(runtime.TrySpawnAvatar(
                    "avatar_main",
                    "smplx_male",
                    new Vector3(0f, 2.5f, 0f),
                    Vector3.zero,
                    out var error), Is.True, error);

                var avatar = runtime.GetManagedAvatarObject();
                Assert.That(avatar, Is.Not.Null);
                Assert.That(avatar.transform.position.y, Is.LessThan(1.0f));
                Assert.That(avatar.transform.position.y, Is.LessThan(2.5f));
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void AvatarMotionLifecycle_UpdatesQueryState()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarMotionLifecycleRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);

                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_wave",
                    "wave_once",
                    "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":120.0,\"betas\":[],\"poses\":[],\"trans\":[]}",
                    "wave once",
                    out var loadError
                ), Is.True, loadError);

                Assert.That(runtime.TryPlayAvatarMotion("avatar_main", 1.25f, false, out var playError), Is.True, playError);

                var queryService = new SceneQueryService(registry, runtime);
                var avatarsResponse = ToObject(queryService.QueryAvatars());
                var motionsResponse = ToObject(queryService.QueryMotions());

                var avatar = avatarsResponse["avatars"]!.ToObject<List<AvatarQueryModel>>()[0];
                var motion = motionsResponse["motions"]!.ToObject<List<AvatarMotionQueryModel>>()[0];

                Assert.That(avatar.motion_id, Is.EqualTo("motion_wave"));
                Assert.That(avatar.motion_name, Is.EqualTo("wave_once"));
                Assert.That(avatar.is_playing, Is.True);
                Assert.That(motion.source_text, Is.EqualTo("wave once"));

                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_walk",
                    "walk_forward",
                    "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":120.0,\"betas\":[],\"poses\":[],\"trans\":[]}",
                    "walk forward",
                    out var replaceError
                ), Is.True, replaceError);

                var replacedResponse = ToObject(queryService.QueryAvatars());
                var replacedAvatar = replacedResponse["avatars"]!.ToObject<List<AvatarQueryModel>>()[0];
                Assert.That(replacedAvatar.motion_id, Is.EqualTo("motion_walk"));
                Assert.That(replacedAvatar.is_playing, Is.False);

                Assert.That(runtime.TryPauseAvatarMotion("avatar_main", out var pauseError), Is.True, pauseError);
                Assert.That(runtime.TryStopAvatarMotion("avatar_main", out var stopError), Is.True, stopError);
                Assert.That(runtime.TryClearAvatarMotion("avatar_main", out var clearError), Is.True, clearError);
                Assert.That(runtime.TryRemoveAvatar("avatar_main", out var removeError), Is.True, removeError);

                var afterResponse = ToObject(queryService.QueryAvatars());
                Assert.That(afterResponse["avatars"]!.ToObject<List<AvatarQueryModel>>().Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void SmplxDriver_CanLoadMotionImmediatelyAfterSpawn()
        {
#if UNITY_EDITOR
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSmplxImmediateLoadRoot");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/smplx/male.prefab");
                Assert.That(prefab, Is.Not.Null, "male.prefab must exist for this integration test.");

                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);

                Assert.That(runtime.TrySpawnAvatar(
                    "avatar_main",
                    "smplx_male",
                    Vector3.zero,
                    Vector3.zero,
                    out var spawnError), Is.True, spawnError);

                const string motionJson =
                    "{\"model\":\"smplx\",\"gender\":\"neutral\",\"fps\":30.0,\"betas\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],\"poses\":[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]],\"trans\":[[0,0,0]]}";

                LogAssert.Expect(LogType.Error, "[SMPL-X] ERROR: Cannot set beta shapes on model without beta shapes");
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_smoke_test",
                    "idle_once",
                    motionJson,
                    "idle",
                    out var loadError), Is.True, loadError);
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
#else
            Assert.Pass("Editor-only integration test.");
#endif
        }

        [Test]
        public void ExecuteActions_SpawnAvatarAndLoadMotion_UpdatesSceneVersion()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarExecuteActionsRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                var controlManager = root.AddComponent<ControlManager>();
                var executor = new SceneTransactionExecutor(registry, controlManager);

                var request = new ActionBatchRequestV2
                {
                    request_id = "req_avatar",
                    scene_version = registry.CurrentVersion,
                    strict = true,
                    actions = new List<ActionCommandV2>
                    {
                        new ActionCommandV2
                        {
                            target_id = string.Empty,
                            action_type = "spawn_avatar",
                            parameters = new Dictionary<string, object>
                            {
                                ["avatar_id"] = "avatar_main",
                                ["prefab_key"] = "smplx_male",
                                ["position"] = new [] { 0f, 0f, 0f },
                                ["rotation"] = new [] { 0f, 0f, 0f },
                            }
                        },
                        new ActionCommandV2
                        {
                            target_id = string.Empty,
                            action_type = "load_avatar_motion",
                            parameters = new Dictionary<string, object>
                            {
                                ["avatar_id"] = "avatar_main",
                                ["motion_id"] = "motion_walk",
                                ["motion_name"] = "walk_forward",
                                ["motion_json"] = "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":120.0,\"betas\":[],\"poses\":[],\"trans\":[]}",
                                ["source_text"] = "walk forward",
                            }
                        }
                    }
                };

                var report = executor.Execute(request);

                Assert.That(report.status, Is.EqualTo("success"));
                Assert.That(report.scene_version_after, Is.GreaterThan(report.scene_version_before));

                var queryService = new SceneQueryService(registry, runtime);
                var avatarsResponse = ToObject(queryService.QueryAvatars());
                var avatars = avatarsResponse["avatars"]!.ToObject<List<AvatarQueryModel>>();
                Assert.That(avatars.Count, Is.EqualTo(1));
                Assert.That(avatars[0].motion_id, Is.EqualTo("motion_walk"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void QueryAvatarCandidates_ReturnsRankedCandidatesNearTarget()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarCandidateRoot");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "CoffeeMaker";
            target.transform.position = new Vector3(0f, 0.5f, 0f);
            target.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
            target.AddComponent<ObjectDescriber>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                var validation = new SceneValidationService(registry, runtime);

                var response = ToObject(validation.QueryAvatarCandidates(
                    targetObjectId: string.Empty,
                    targetAlias: "CoffeeMaker",
                    taskHint: "make_coffee",
                    preferredSide: "front",
                    preferredDistance: null,
                    maxCandidates: 3));

                var candidates = response["candidates"]!.ToObject<List<AvatarCandidateQueryModel>>();
                Assert.That(candidates.Count, Is.GreaterThan(0));
                Assert.That(candidates[0].target_object_id, Is.Not.Empty);
                Assert.That(candidates[0].distance_to_target, Is.GreaterThan(0.4f));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void QueryAvatarCandidates_PrefersFloorHeightOverCabinetTop()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarCandidateFloorSnapRoot");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "CoffeeMaker";
            target.transform.position = new Vector3(0f, 1.2f, 0f);
            target.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            target.AddComponent<ObjectDescriber>();

            var cabinetTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabinetTop.name = "CabinetTop";
            cabinetTop.transform.position = new Vector3(0f, 1.0f, 0.8f);
            cabinetTop.transform.localScale = new Vector3(1.0f, 0.1f, 1.0f);
            cabinetTop.AddComponent<ObjectDescriber>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                var validation = new SceneValidationService(registry, runtime);

                var response = ToObject(validation.QueryAvatarCandidates(
                    targetObjectId: string.Empty,
                    targetAlias: "CoffeeMaker",
                    taskHint: "make_coffee",
                    preferredSide: "front",
                    preferredDistance: 0.9f,
                    maxCandidates: 3));

                var candidates = response["candidates"]!.ToObject<List<AvatarCandidateQueryModel>>();
                Assert.That(candidates.Count, Is.GreaterThan(0));
                Assert.That(candidates[0].position.y, Is.LessThan(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cabinetTop);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void QueryAvatarCandidates_ClampCandidatesToFloorFootprint()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarCandidateFootprintRoot");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "CoffeeMaker";
            target.transform.position = new Vector3(0f, 0.4f, 0.95f);
            target.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            target.AddComponent<ObjectDescriber>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(2f, 0.1f, 2f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                var validation = new SceneValidationService(registry, runtime);

                var response = ToObject(validation.QueryAvatarCandidates(
                    targetObjectId: string.Empty,
                    targetAlias: "CoffeeMaker",
                    taskHint: "make_coffee",
                    preferredSide: "front",
                    preferredDistance: 0.9f,
                    maxCandidates: 3));

                var candidates = response["candidates"]!.ToObject<List<AvatarCandidateQueryModel>>();
                Assert.That(candidates.Count, Is.GreaterThan(0));
                Assert.That(Mathf.Abs(candidates[0].position.x), Is.LessThanOrEqualTo(1.0f));
                Assert.That(Mathf.Abs(candidates[0].position.z), Is.LessThanOrEqualTo(1.0f));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void QueryAvatarCandidates_AddsDiagonalFallbacksWhenPreferredFrontIsBlocked()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarCandidateDiagonalFallbackRoot");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "CoffeeMaker";
            target.transform.position = new Vector3(0f, 0.5f, 0f);
            target.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
            target.AddComponent<ObjectDescriber>();

            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "FrontCounterBlocker";
            obstacle.transform.position = new Vector3(0f, 0.65f, 0.9f);
            obstacle.transform.localScale = new Vector3(1.2f, 1.3f, 0.8f);
            obstacle.AddComponent<ObjectDescriber>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                var validation = new SceneValidationService(registry, runtime);

                var response = ToObject(validation.QueryAvatarCandidates(
                    targetObjectId: string.Empty,
                    targetAlias: "CoffeeMaker",
                    taskHint: "make_coffee",
                    preferredSide: "front",
                    preferredDistance: 0.9f,
                    maxCandidates: 6));

                var candidates = response["candidates"]!.ToObject<List<AvatarCandidateQueryModel>>();
                Assert.That(candidates.Count, Is.GreaterThanOrEqualTo(6));
                Assert.That(candidates.Exists(candidate =>
                    candidate.preferred_side == "front_left" ||
                    candidate.preferred_side == "front_right"), Is.True);
                Assert.That(candidates[0].preferred_side, Is.Not.EqualTo("front"));
                Assert.That(candidates[0].collision_free, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(obstacle);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void ValidateAvatarPlacement_FailsWhenEmbeddedInTarget()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarValidateRoot");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "CoffeeMaker";
            target.transform.position = new Vector3(0f, 0.5f, 0f);
            target.transform.localScale = new Vector3(0.6f, 1f, 0.6f);
            target.AddComponent<ObjectDescriber>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                var validation = new SceneValidationService(registry, runtime);

                var response = ToObject(validation.ValidateAvatarPlacement(
                    targetObjectId: string.Empty,
                    targetAlias: "CoffeeMaker",
                    taskHint: "make_coffee",
                    position: new Vector3(0f, 0f, 0f),
                    rotationEuler: new Vector3(0f, 0f, 0f),
                    motionJson: string.Empty,
                    trajectorySampleCount: null));

                var report = response["validation"]!.ToObject<AvatarPlacementValidationModel>();
                Assert.That(report.valid, Is.False);
                Assert.That(report.issues.Exists(issue => issue.code == "AVATAR_EMBEDDED_TARGET"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void ValidateAvatarPlacement_UsesSnapshotBoundsWhenColliderIsMissing()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSnapshotCollisionRoot");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "CoffeeMaker";
            target.transform.position = new Vector3(0f, 0.5f, 0f);
            target.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
            target.AddComponent<ObjectDescriber>();

            var cabinet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabinet.name = "CabinetBody";
            cabinet.transform.position = new Vector3(0.8f, 0.9f, 0f);
            cabinet.transform.localScale = new Vector3(0.8f, 1.8f, 0.8f);
            cabinet.AddComponent<ObjectDescriber>();
            Object.DestroyImmediate(cabinet.GetComponent<Collider>());

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                var validation = new SceneValidationService(registry, runtime);

                var response = ToObject(validation.ValidateAvatarPlacement(
                    targetObjectId: string.Empty,
                    targetAlias: "CoffeeMaker",
                    taskHint: "make_coffee",
                    position: new Vector3(0.8f, 0f, 0f),
                    rotationEuler: Vector3.zero,
                    motionJson: string.Empty,
                    trajectorySampleCount: null));

                var report = response["validation"]!.ToObject<AvatarPlacementValidationModel>();
                Assert.That(report.valid, Is.False);
                Assert.That(report.collision_free, Is.False);
                Assert.That(report.issues.Exists(issue => issue.code == SceneApiErrorCodes.PLACEMENT_COLLISION), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cabinet);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void ValidateAvatarPlacement_DetectsTrajectoryCollision()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarTrajectoryRoot");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Door_A";
            target.transform.position = new Vector3(1.2f, 0.5f, 0f);
            target.transform.localScale = new Vector3(0.8f, 2f, 0.2f);
            target.AddComponent<ObjectDescriber>();

            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "Obstacle";
            obstacle.transform.position = new Vector3(0.2f, 0.9f, 0f);
            obstacle.transform.localScale = new Vector3(0.4f, 1.8f, 0.4f);
            obstacle.AddComponent<ObjectDescriber>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            const string motionJson = "{\"model\":\"smplx\",\"gender\":\"male\",\"fps\":30.0,\"betas\":[],\"poses\":[[],[],[]],\"trans\":[[0,0,0],[0.4,0,0],[0.8,0,0]]}";

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                var validation = new SceneValidationService(registry, runtime);

                var response = ToObject(validation.ValidateAvatarPlacement(
                    targetObjectId: string.Empty,
                    targetAlias: "Door_A",
                    taskHint: "walk_through_door",
                    position: new Vector3(0f, 0f, 0f),
                    rotationEuler: new Vector3(0f, 90f, 0f),
                    motionJson: motionJson,
                    trajectorySampleCount: 5));

                var report = response["validation"]!.ToObject<AvatarPlacementValidationModel>();
                Assert.That(report.motion_mode, Is.EqualTo("translational"));
                Assert.That(report.trajectory_valid, Is.False);
                Assert.That(report.issues.Exists(issue => issue.code == "TRAJECTORY_COLLISION"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(obstacle);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void SmplxGlobalTranslation_PreservesForwardDirection()
        {
#if UNITY_EDITOR
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSmplxForwardTranslationRoot");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/smplx/male.prefab");
                Assert.That(prefab, Is.Not.Null, "male.prefab must exist for this translation test.");

                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TrySpawnAvatar(
                    "avatar_main",
                    "smplx_male",
                    Vector3.zero,
                    Vector3.zero,
                    out var spawnError), Is.True, spawnError);

                const string motionJson =
                    "{\"model\":\"smplx\",\"gender\":\"neutral\",\"fps\":30.0,\"betas\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],\"poses\":[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]],\"trans\":[[0,0,0],[0,0,1]]}";

                LogAssert.Expect(LogType.Error, "[SMPL-X] ERROR: Cannot set beta shapes on model without beta shapes");
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_forward",
                    "walk_forward",
                    motionJson,
                    "walk forward",
                    out var loadError), Is.True, loadError);

                var controller = runtime.GetManagedAvatarObject().GetComponentInChildren<smplx.SmplxBodyAnimationController>(true);
                Assert.That(controller, Is.Not.Null);

                Vector3 translation = controller.getGlobalTranslation(controller.deltaTime);
                Assert.That(translation.z, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
#else
            Assert.Pass("Editor-only integration test.");
#endif
        }

        [Test]
        public void SmplxGlobalTranslation_MirrorsXAxisToMatchUnityWorld()
        {
#if UNITY_EDITOR
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSmplxXAxisTranslationRoot");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/smplx/male.prefab");
                Assert.That(prefab, Is.Not.Null, "male.prefab must exist for this translation test.");

                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TrySpawnAvatar(
                    "avatar_main",
                    "smplx_male",
                    Vector3.zero,
                    Vector3.zero,
                    out var spawnError), Is.True, spawnError);

                const string motionJson =
                    "{\"model\":\"smplx\",\"gender\":\"neutral\",\"fps\":30.0,\"betas\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],\"poses\":[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]],\"trans\":[[0,0,0],[1,0,0]]}";

                LogAssert.Expect(LogType.Error, "[SMPL-X] ERROR: Cannot set beta shapes on model without beta shapes");
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_right",
                    "step_right",
                    motionJson,
                    "step right",
                    out var loadError), Is.True, loadError);

                var controller = runtime.GetManagedAvatarObject().GetComponentInChildren<smplx.SmplxBodyAnimationController>(true);
                Assert.That(controller, Is.Not.Null);

                Vector3 translation = controller.getGlobalTranslation(controller.deltaTime);
                Assert.That(translation.x, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
#else
            Assert.Pass("Editor-only integration test.");
#endif
        }

        [Test]
        public void SmplxAvatarRotation_AlignsVisibleForwardWithRequestedYaw()
        {
#if UNITY_EDITOR
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSmplxRotationAlignmentRoot");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();

            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/smplx/male.prefab");
                Assert.That(prefab, Is.Not.Null, "male.prefab must exist for this rotation test.");

                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TrySpawnAvatar(
                    "avatar_main",
                    "smplx_male",
                    Vector3.zero,
                    Vector3.zero,
                    out var spawnError), Is.True, spawnError);

                var avatar = runtime.GetManagedAvatarObject();
                Assert.That(avatar, Is.Not.Null);

                const string motionJson =
                    "{\"model\":\"smplx\",\"gender\":\"neutral\",\"fps\":30.0,\"betas\":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],\"poses\":[[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]],\"trans\":[[0,0,0]]}";
                LogAssert.Expect(LogType.Error, "[SMPL-X] ERROR: Cannot set beta shapes on model without beta shapes");
                Assert.That(runtime.TryLoadAvatarMotion(
                    "avatar_main",
                    "motion_alignment_probe",
                    "alignment_probe",
                    motionJson,
                    "alignment probe",
                    out var loadError), Is.True, loadError);

                var controller = avatar.GetComponentInChildren<smplx.SmplxBodyAnimationController>(true);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.Root, Is.Not.Null);

                var visibleForward = controller.Root.forward.normalized;
                Assert.That(Vector3.Dot(visibleForward, Vector3.forward), Is.GreaterThan(0.99f));
            }
            finally
            {
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
#else
            Assert.Pass("Editor-only integration test.");
#endif
        }

        [Test]
        public void CaptureValidationViews_ReturnsAvatarEyeAndUserCameraArtifacts()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarCaptureRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "CoffeeMaker";
            target.transform.position = new Vector3(0f, 0.5f, 0f);
            target.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
            target.AddComponent<ObjectDescriber>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "KitchenFloor";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
            floor.AddComponent<ObjectDescriber>();
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TrySpawnAvatar("avatar_main", "smplx_male", new Vector3(-1f, 0f, 0f), Vector3.zero, out var spawnError), Is.True, spawnError);

                var validation = new SceneValidationService(registry, runtime);
                var response = ToObject(validation.CaptureValidationViews("avatar_test", "avatar_main", string.Empty, "CoffeeMaker"));

                var artifacts = response["artifacts"]!.ToObject<List<ValidationArtifactModel>>();
                Assert.That(artifacts.Count, Is.EqualTo(2));
                Assert.That(artifacts[0].label, Is.EqualTo("avatar_eye"));
                Assert.That(artifacts[1].label, Is.EqualTo("user_camera"));
                Assert.That(File.Exists(artifacts[0].file_path), Is.True);
                Assert.That(File.Exists(artifacts[1].file_path), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }
    }

    public class TestAvatarPlaybackDriver : AvatarPlaybackDriver
    {
        public override bool TryLoadMotion(string motionId, string motionName, string motionJson, out string error)
        {
            LoadedMotionId = motionId;
            LoadedMotionName = motionName;
            HasLoadedMotion = true;
            LastMotionJson = motionJson;
            error = null;
            return true;
        }
    }

    public class TestVirtualSensor : Sensor.VirtualSensor
    {
        private static readonly Sensor.ISensorDefinition Definition = Sensor.ISensorDefinition.create("IMU", "x,y,z");

        public override void UpdateWorking(float time, float deltaTime)
        {
        }

        public override Sensor.ISensorDefinition SensorDefinition()
        {
            return Definition;
        }
    }

    internal static class AvatarSceneApiTestHelpers
    {
        internal static GameObject CreateAttachmentAwareAvatarPrefab()
        {
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

            CreateNamedJoint(prefab.transform, "pelvis", new Vector3(0f, 0.9f, 0f));
            CreateNamedJoint(prefab.transform, "spine1", new Vector3(0f, 1.0f, 0f));
            CreateNamedJoint(prefab.transform, "spine2", new Vector3(0f, 1.15f, 0f));
            CreateNamedJoint(prefab.transform, "spine3", new Vector3(0f, 1.3f, 0f));
            CreateNamedJoint(prefab.transform, "neck", new Vector3(0f, 1.5f, 0f));
            CreateNamedJoint(prefab.transform, "head", new Vector3(0f, 1.65f, 0f));
            CreateNamedJoint(prefab.transform, "left_wrist", new Vector3(-0.35f, 1.2f, 0f));
            CreateNamedJoint(prefab.transform, "right_wrist", new Vector3(0.35f, 1.2f, 0f));
            CreateNamedJoint(prefab.transform, "left_ankle", new Vector3(-0.12f, 0.08f, 0f));
            CreateNamedJoint(prefab.transform, "right_ankle", new Vector3(0.12f, 0.08f, 0f));

            return prefab;
        }

        internal static GameObject CreateImuSensorPrefab()
        {
            var sensorPrefab = new GameObject("IMUPrefab");
            sensorPrefab.AddComponent<TestVirtualSensor>();
            sensorPrefab.AddComponent<SensorObjectDescriber>();
            return sensorPrefab;
        }

        private static Transform CreateNamedJoint(Transform parent, string name, Vector3 localPosition)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = localPosition;
            return joint;
        }
    }
}
