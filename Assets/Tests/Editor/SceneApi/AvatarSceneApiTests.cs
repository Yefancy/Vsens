using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VsensAgent;
using VsensAgent.Core;
using VsensAgent.Network.Protocol;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.Tests.Editor.SceneApi
{
    public class AvatarSceneApiTests
    {
        [Test]
        public void QueryAvatars_ReturnsSpawnedAvatarInSceneModel()
        {
            ServiceLocator.Clear();
            var root = new GameObject("AvatarSceneApiRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

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
                dynamic response = queryService.QueryAvatars();

                Assert.That((string)response.method, Is.EqualTo("scene.query_avatars"));
                Assert.That(((IList<AvatarQueryModel>)response.avatars).Count, Is.EqualTo(1));
                Assert.That(((IList<AvatarQueryModel>)response.avatars)[0].avatar_id, Is.EqualTo("avatar_main"));

                var snapshot = registry.BuildSnapshot(includeRelations: false);
                Assert.That(snapshot.objects.Exists(o => o.alias == "avatar_main"), Is.True);
            }
            finally
            {
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
                dynamic avatarsResponse = queryService.QueryAvatars();
                dynamic motionsResponse = queryService.QueryMotions();

                var avatar = ((IList<AvatarQueryModel>)avatarsResponse.avatars)[0];
                var motion = ((IList<AvatarMotionQueryModel>)motionsResponse.motions)[0];

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

                dynamic replacedResponse = queryService.QueryAvatars();
                var replacedAvatar = ((IList<AvatarQueryModel>)replacedResponse.avatars)[0];
                Assert.That(replacedAvatar.motion_id, Is.EqualTo("motion_walk"));
                Assert.That(replacedAvatar.is_playing, Is.False);

                Assert.That(runtime.TryPauseAvatarMotion("avatar_main", out var pauseError), Is.True, pauseError);
                Assert.That(runtime.TryStopAvatarMotion("avatar_main", out var stopError), Is.True, stopError);
                Assert.That(runtime.TryClearAvatarMotion("avatar_main", out var clearError), Is.True, clearError);
                Assert.That(runtime.TryRemoveAvatar("avatar_main", out var removeError), Is.True, removeError);

                dynamic afterResponse = queryService.QueryAvatars();
                Assert.That(((IList<AvatarQueryModel>)afterResponse.avatars).Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
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
                dynamic avatarsResponse = queryService.QueryAvatars();
                Assert.That(((IList<AvatarQueryModel>)avatarsResponse.avatars).Count, Is.EqualTo(1));
                Assert.That(((IList<AvatarQueryModel>)avatarsResponse.avatars)[0].motion_id, Is.EqualTo("motion_walk"));
            }
            finally
            {
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
}
