using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.RuntimeEditing;
using VsensAgent.SceneApi.V2;
using VsensAgent.Tests.Editor.SceneApi;

namespace VsensAgent.Tests.Editor.RuntimeEditing
{
    public class RuntimeEditModeTests
    {
        [Test]
        public void RuntimeEditMode_MoveAndRotateAvatar_UpdatesPoseAndAuthority()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditModeRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

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
                    new Vector3(0f, 0f, 0f),
                    Vector3.zero,
                    out var spawnError), Is.True, spawnError);

                var controller = root.AddComponent<RuntimeEditModeController>();
                controller.Configure(runtime);

                Assert.That(controller.IsEditModeEnabled, Is.False);
                controller.SetEditMode(true);
                Assert.That(controller.IsEditModeEnabled, Is.True);
                Assert.That(controller.TrySelectEditable("avatar_main"), Is.True);

                Assert.That(controller.TryMoveSelectionToGroundPoint(new Vector3(1.5f, 3f, 2.5f)), Is.True);
                Assert.That(controller.TryRotateSelectionYaw(135f), Is.True);

                var queryService = new SceneQueryService(registry, runtime);
                var response = Newtonsoft.Json.Linq.JObject.FromObject(queryService.QueryAvatars());
                var avatars = response["avatars"]!.ToObject<List<AvatarQueryModel>>();
                var avatarObject = runtime.GetManagedAvatarObject();

                Assert.That(avatars, Has.Count.EqualTo(1));
                Assert.That(avatars[0].pose_authority, Is.EqualTo("manual"));
                Assert.That(avatars[0].position.x, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(avatars[0].position.y, Is.LessThan(3f));
                Assert.That(avatarObject, Is.Not.Null);
                Assert.That(avatars[0].position.y, Is.EqualTo(avatarObject.transform.position.y).Within(0.001f));
                Assert.That(avatars[0].position.z, Is.EqualTo(2.5f).Within(0.001f));
                Assert.That(avatars[0].rotation.y, Is.EqualTo(135f).Within(0.001f));
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
        public void RuntimeEditMode_DisablingClearsSelectionButPreservesManualPose()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditModeDisableRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

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
                    new Vector3(0f, 0f, 0f),
                    Vector3.zero,
                    out var spawnError), Is.True, spawnError);

                var controller = root.AddComponent<RuntimeEditModeController>();
                controller.Configure(runtime);
                controller.SetEditMode(true);
                Assert.That(controller.TrySelectEditable("avatar_main"), Is.True);
                Assert.That(controller.TryMoveSelectionToGroundPoint(new Vector3(2f, 1f, 1f)), Is.True);

                controller.SetEditMode(false);
                Assert.That(controller.IsEditModeEnabled, Is.False);
                Assert.That(controller.SelectedObjectId, Is.EqualTo(string.Empty));

                var queryService = new SceneQueryService(registry, runtime);
                var response = Newtonsoft.Json.Linq.JObject.FromObject(queryService.QueryAvatars());
                var avatars = response["avatars"]!.ToObject<List<AvatarQueryModel>>();

                Assert.That(avatars[0].pose_authority, Is.EqualTo("manual"));
                Assert.That(avatars[0].position.x, Is.EqualTo(2f).Within(0.001f));
                Assert.That(avatars[0].position.z, Is.EqualTo(1f).Within(0.001f));
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
        public void RuntimeEditMode_TryHandlePointerRay_MovesAvatarWithoutFloorCollider()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditPointerRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

            try
            {
                var registry = root.AddComponent<SceneRegistry>();
                var runtime = root.AddComponent<AvatarRuntimeManager>();
                runtime.ConfigureDefaultPrefab(prefab);
                Assert.That(runtime.TrySpawnAvatar(
                    "avatar_main",
                    "smplx_male",
                    new Vector3(0f, 0f, 0f),
                    Vector3.zero,
                    out var spawnError), Is.True, spawnError);

                var controller = root.AddComponent<RuntimeEditModeController>();
                controller.Configure(runtime);
                controller.SetEditMode(true);
                Assert.That(controller.TrySelectEditable("avatar_main"), Is.True);

                var cameraGo = new GameObject("RuntimeEditCamera");
                var camera = cameraGo.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 3f, -4f);
                camera.transform.LookAt(new Vector3(2f, 0f, 2f));
                controller.OverrideRuntimeCameraForTests(camera);

                var ray = new Ray(camera.transform.position, (new Vector3(2f, 0f, 2f) - camera.transform.position).normalized);

                Assert.That(controller.TryHandlePointerRay(ray), Is.True);

                var queryService = new SceneQueryService(registry, runtime);
                var response = Newtonsoft.Json.Linq.JObject.FromObject(queryService.QueryAvatars());
                var avatars = response["avatars"]!.ToObject<List<AvatarQueryModel>>();

                Assert.That(avatars[0].pose_authority, Is.EqualTo("manual"));
                Assert.That(avatars[0].position.x, Is.EqualTo(2f).Within(0.1f));
                Assert.That(avatars[0].position.z, Is.EqualTo(2f).Within(0.1f));

                Object.DestroyImmediate(cameraGo);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }
    }
}
