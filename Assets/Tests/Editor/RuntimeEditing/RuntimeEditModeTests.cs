using System.Collections.Generic;
using NUnit.Framework;
using Sensor;
using TransformHandles;
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
        public void RuntimeEditMode_TryHandlePointerRay_DoesNotMoveSelectedAvatar()
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

                Assert.That(controller.TryHandlePointerRay(ray), Is.False);

                var queryService = new SceneQueryService(registry, runtime);
                var response = Newtonsoft.Json.Linq.JObject.FromObject(queryService.QueryAvatars());
                var avatars = response["avatars"]!.ToObject<List<AvatarQueryModel>>();

                Assert.That(avatars[0].pose_authority, Is.EqualTo("agent"));
                Assert.That(avatars[0].position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(avatars[0].position.z, Is.EqualTo(0f).Within(0.001f));

                Object.DestroyImmediate(cameraGo);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RuntimeEditMode_TryHandlePointerRay_DoesNotSelectAvatarFromSceneClick()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditAvatarSceneSelectRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

            try
            {
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

                var avatarObject = runtime.GetManagedAvatarObject();
                Assert.That(avatarObject, Is.Not.Null);
                var collider = avatarObject.GetComponent<Collider>();
                if (collider == null)
                {
                    collider = avatarObject.AddComponent<BoxCollider>();
                }

                var ray = new Ray(avatarObject.transform.position + new Vector3(0f, 0f, -5f), Vector3.forward);
                Assert.That(controller.TryHandlePointerRay(ray), Is.False);
                Assert.That(controller.SelectedObjectId, Is.EqualTo(string.Empty));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RuntimeEditMode_TryHandlePointerRay_DoesNotSwitchFromSensorToAvatar()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditAvatarSceneSwitchRoot");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

            try
            {
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

                var sensorObject = new GameObject("IMU-AvatarSwitchGuard");
                sensorObject.AddComponent<TestEditableSensor>();

                Assert.That(controller.TrySelectEditable("IMU-AvatarSwitchGuard"), Is.True);
                Assert.That(controller.SelectedObjectId, Is.EqualTo("IMU-AvatarSwitchGuard"));

                var avatarObject = runtime.GetManagedAvatarObject();
                Assert.That(avatarObject, Is.Not.Null);
                var collider = avatarObject.GetComponent<Collider>();
                if (collider == null)
                {
                    collider = avatarObject.AddComponent<BoxCollider>();
                }

                var ray = new Ray(avatarObject.transform.position + new Vector3(0f, 0f, -5f), Vector3.forward);
                Assert.That(controller.TryHandlePointerRay(ray), Is.False);
                Assert.That(controller.SelectedObjectId, Is.EqualTo("IMU-AvatarSwitchGuard"));

                Object.DestroyImmediate(sensorObject);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RuntimeEditMode_CanSelectSensorWithoutMutatingTransform()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditSensorRoot");

            try
            {
                root.AddComponent<SceneRegistry>();
                var controller = root.AddComponent<RuntimeEditModeController>();
                controller.SetEditMode(true);

                var sensorObject = new GameObject("IMU-01");
                var sensor = sensorObject.AddComponent<TestEditableSensor>();
                sensor.transform.position = new Vector3(0f, 1.2f, 0f);

                Assert.That(controller.TrySelectEditable("IMU-01"), Is.True);
                Assert.That(controller.SelectedObjectId, Is.EqualTo("IMU-01"));
                Assert.That(controller.TryMoveSelectionToGroundPoint(new Vector3(2f, 99f, 3f)), Is.False);
                Assert.That(controller.TryRotateSelectionYaw(45f), Is.False);

                Assert.That(sensor.transform.position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(sensor.transform.position.y, Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(sensor.transform.position.z, Is.EqualTo(0f).Within(0.001f));
                Assert.That(sensor.transform.rotation.eulerAngles.y, Is.EqualTo(0f).Within(0.001f));

                Object.DestroyImmediate(sensorObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RuntimeEditMode_SelectingSensor_CreatesRuntimeTransformHandle()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditSensorHandleRoot");
            var cameraGo = new GameObject("RuntimeEditHandleCamera");

            try
            {
                var camera = cameraGo.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 2f, -4f);
                camera.transform.LookAt(Vector3.zero);

                root.AddComponent<SceneRegistry>();
                var controller = root.AddComponent<RuntimeEditModeController>();
                controller.OverrideRuntimeCameraForTests(camera);
                controller.SetEditMode(true);

                var sensorObject = new GameObject("IMU-02");
                sensorObject.AddComponent<TestEditableSensor>();

                Assert.That(controller.TrySelectEditable("IMU-02"), Is.True);

                var bridge = root.GetComponent<RuntimeTransformHandleBridge>();
                Assert.That(bridge, Is.Not.Null);
                bridge.RefreshHandleBinding();

                var handle = bridge.ActiveHandle ?? Object.FindFirstObjectByType<Handle>();
                Assert.That(handle, Is.Not.Null);
                Assert.That(handle.type, Is.EqualTo(HandleType.Position));
                Assert.That(handle.target, Is.EqualTo(sensorObject.transform));

                Object.DestroyImmediate(sensorObject);
                if (handle != null)
                {
                    Object.DestroyImmediate(handle.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                var manager = Object.FindFirstObjectByType<TransformHandleManager>();
                if (manager != null)
                {
                    Object.DestroyImmediate(manager.gameObject);
                }

                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RuntimeEditMode_SelectingAvatar_CreatesRuntimeTransformHandle()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditAvatarHandleRoot");
            var cameraGo = new GameObject("RuntimeEditAvatarHandleCamera");
            var prefab = new GameObject("AvatarPrefab");
            prefab.AddComponent<TestAvatarPlaybackDriver>();

            try
            {
                var camera = cameraGo.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 2f, -4f);
                camera.transform.LookAt(Vector3.zero);

                root.AddComponent<SceneRegistry>();
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
                controller.OverrideRuntimeCameraForTests(camera);
                controller.SetEditMode(true);

                Assert.That(controller.TrySelectEditable("avatar_main"), Is.True);

                var bridge = root.GetComponent<RuntimeTransformHandleBridge>();
                Assert.That(bridge, Is.Not.Null);
                Assert.That(bridge.SupportsCurrentSelection, Is.True);
                bridge.RefreshHandleBinding();

                var handle = bridge.ActiveHandle ?? Object.FindFirstObjectByType<Handle>();
                var avatarObject = runtime.GetManagedAvatarObject();
                Assert.That(handle, Is.Not.Null);
                Assert.That(handle.type, Is.EqualTo(HandleType.Position));
                Assert.That(handle.target, Is.EqualTo(avatarObject.transform));

                if (handle != null)
                {
                    Object.DestroyImmediate(handle.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                var manager = Object.FindFirstObjectByType<TransformHandleManager>();
                if (manager != null)
                {
                    Object.DestroyImmediate(manager.gameObject);
                }

                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RuntimeEditMode_ChangingHandleType_UpdatesActiveHandle()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditSensorHandleModeRoot");
            var cameraGo = new GameObject("RuntimeEditHandleModeCamera");

            try
            {
                var camera = cameraGo.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 2f, -4f);
                camera.transform.LookAt(Vector3.zero);

                root.AddComponent<SceneRegistry>();
                var controller = root.AddComponent<RuntimeEditModeController>();
                controller.OverrideRuntimeCameraForTests(camera);
                controller.SetEditMode(true);

                var sensorObject = new GameObject("IMU-03");
                sensorObject.AddComponent<TestEditableSensor>();

                Assert.That(controller.TrySelectEditable("IMU-03"), Is.True);

                var bridge = root.GetComponent<RuntimeTransformHandleBridge>();
                Assert.That(bridge, Is.Not.Null);
                bridge.RefreshHandleBinding();
                Assert.That(bridge.ActiveHandle, Is.Not.Null);
                Assert.That(bridge.ActiveHandle.type, Is.EqualTo(HandleType.Position));

                bridge.SetHandleType(HandleType.Rotation);

                Assert.That(bridge.ActiveHandle, Is.Not.Null);
                Assert.That(bridge.ActiveHandle.type, Is.EqualTo(HandleType.Rotation));

                Object.DestroyImmediate(sensorObject);
                if (bridge.ActiveHandle != null)
                {
                    Object.DestroyImmediate(bridge.ActiveHandle.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                var manager = Object.FindFirstObjectByType<TransformHandleManager>();
                if (manager != null)
                {
                    Object.DestroyImmediate(manager.gameObject);
                }

                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        [Test]
        public void RuntimeTransformHandleBridge_RecordsSensorTransformChangeOnInteractionEnd()
        {
            ServiceLocator.Clear();
            var root = new GameObject("RuntimeEditSensorHistoryRoot");
            var cameraGo = new GameObject("RuntimeEditHistoryCamera");

            try
            {
                var camera = cameraGo.AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0f, 2f, -4f);
                camera.transform.LookAt(Vector3.zero);

                root.AddComponent<SceneRegistry>();
                var history = root.AddComponent<VsensAgent.SceneHistory.SceneActionHistory>();
                var controller = root.AddComponent<RuntimeEditModeController>();
                controller.OverrideRuntimeCameraForTests(camera);
                controller.SetEditMode(true);

                var sensorObject = new GameObject("IMU-History");
                sensorObject.AddComponent<TestEditableSensor>();

                Assert.That(controller.TrySelectEditable("IMU-History"), Is.True);

                var bridge = root.GetComponent<RuntimeTransformHandleBridge>();
                Assert.That(bridge, Is.Not.Null);
                bridge.RefreshHandleBinding();
                bridge.CaptureInteractionStartForTests();

                sensorObject.transform.position = new Vector3(1f, 2f, 3f);
                bridge.CompleteInteractionForTests();

                Assert.That(history.OperationLog.Count, Is.EqualTo(1));
                Assert.That(history.OperationLog[0].source, Is.EqualTo("user"));
                Assert.That(history.OperationLog[0].actionType, Is.EqualTo("set_sensor"));
                Assert.That(history.OperationLog[0].targetId, Is.EqualTo("IMU-History"));

                Object.DestroyImmediate(sensorObject);
                if (bridge.ActiveHandle != null)
                {
                    Object.DestroyImmediate(bridge.ActiveHandle.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                var manager = Object.FindFirstObjectByType<TransformHandleManager>();
                if (manager != null)
                {
                    Object.DestroyImmediate(manager.gameObject);
                }

                Object.DestroyImmediate(root);
                ServiceLocator.Clear();
            }
        }

        private class TestEditableSensor : VirtualSensor
        {
            public override void UpdateWorking(float time, float deltaTime)
            {
            }

            public override ISensorDefinition SensorDefinition()
            {
                return ISensorDefinition.create("TEST_SENSOR", "value");
            }
        }
    }
}
