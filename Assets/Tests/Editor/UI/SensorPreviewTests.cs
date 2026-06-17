using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sensor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VsensAgent.UI;
using VsensAgent.UI.Sensor;
using VsensAgent.VirtualObject.Sensor;

namespace VsensAgent.Tests.Editor.UI
{
    public class SensorPreviewTests
    {
        private static FieldInfo ResolvePreviewInstanceField()
        {
            var field = typeof(SensorPreview).GetField("previewInstance", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field;
        }

        [Test]
        public void BeginPlacementAndCancel_ToggleWorkspaceAndPreview()
        {
            var root = new GameObject("SensorPreviewRoot");
            var workspace = new GameObject("Workspace");
            var workspaceCanvasGroup = workspace.AddComponent<CanvasGroup>();
            var sensorPrefab = new GameObject("SensorPrefab");

            try
            {
                root.AddComponent<EventSystem>();
                root.AddComponent<StandaloneInputModule>();

                var preview = root.AddComponent<SensorPreview>();
                preview.workspaceCanvasGroup = workspaceCanvasGroup;
                preview.sensorPrefab = sensorPrefab.AddComponent<TestPlacementSensor>();
                preview.addButton = new GameObject("AddButton").AddComponent<Button>();

                preview.BeginPlacement();

                Assert.That(preview.IsPlacing, Is.True);
                Assert.That(workspaceCanvasGroup.alpha, Is.EqualTo(0f));
                Assert.That(workspaceCanvasGroup.interactable, Is.False);
                Assert.That(workspaceCanvasGroup.blocksRaycasts, Is.False);
                Assert.That(ResolvePreviewInstanceField().GetValue(preview), Is.Not.Null);

                preview.CancelPlacement();

                Assert.That(preview.IsPlacing, Is.False);
                Assert.That(workspaceCanvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(workspaceCanvasGroup.interactable, Is.True);
                Assert.That(workspaceCanvasGroup.blocksRaycasts, Is.True);
                Assert.That(ResolvePreviewInstanceField().GetValue(preview), Is.Null);

                Object.DestroyImmediate(preview.addButton.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(sensorPrefab);
                Object.DestroyImmediate(workspace);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryPlaceSensorAtHit_ParentsSensorAndAlignsForwardToNormal()
        {
            var root = new GameObject("SensorPlacementRoot");
            var workspace = new GameObject("Workspace");
            var workspaceCanvasGroup = workspace.AddComponent<CanvasGroup>();
            var sensorPrefab = new GameObject("SensorPrefab");
            var cameraObject = new GameObject("MainCamera");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                root.AddComponent<EventSystem>();
                root.AddComponent<StandaloneInputModule>();

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = Vector3.zero;
                cameraObject.transform.rotation = Quaternion.identity;

                target.name = "PlacementTarget";
                target.transform.position = new Vector3(0f, 0f, 5f);
                Physics.SyncTransforms();

                var preview = root.AddComponent<SensorPreview>();
                preview.workspaceCanvasGroup = workspaceCanvasGroup;
                preview.sensorPrefab = sensorPrefab.AddComponent<TestPlacementSensor>();

                preview.BeginPlacement();
                Assert.That(Physics.Raycast(new Ray(Vector3.zero, Vector3.forward), out var hit, 20f), Is.True, "Expected test raycast to hit target collider.");

                var placed = preview.TryPlaceSensorAtHit(hit);

                Assert.That(placed, Is.True);
                Assert.That(preview.IsPlacing, Is.False);
                Assert.That(workspaceCanvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(workspaceCanvasGroup.interactable, Is.True);
                Assert.That(workspaceCanvasGroup.blocksRaycasts, Is.True);

                var sensors = Object.FindObjectsByType<TestPlacementSensor>(FindObjectsSortMode.None);
                var created = sensors.First(sensor => sensor != preview.sensorPrefab);
                Assert.That(created.transform.parent, Is.EqualTo(hit.collider.transform));
                Assert.That(Vector3.Dot(created.transform.forward.normalized, hit.normal.normalized), Is.GreaterThan(0.999f));
            }
            finally
            {
                foreach (var sensor in Object.FindObjectsByType<TestPlacementSensor>(FindObjectsSortMode.None))
                {
                    if (sensor != null)
                    {
                        Object.DestroyImmediate(sensor.gameObject);
                    }
                }

                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sensorPrefab);
                Object.DestroyImmediate(workspace);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryPlaceSensorAtHit_AssignsUniqueNameAndRefreshesPanel()
        {
            var root = new GameObject("SensorPlacementPanelRoot");
            var workspace = new GameObject("Workspace");
            var workspaceCanvasGroup = workspace.AddComponent<CanvasGroup>();
            var sensorPrefab = new GameObject("SensorPrefab");
            var cameraObject = new GameObject("MainCamera");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var panelRoot = new GameObject("SensorPanelRoot");
            var itemContainer = new GameObject("ItemContainer").transform;
            var sensorItemPrefab = CreateSensorItemPrefab();
            var existingSensorObject = new GameObject("IMU-01");
            var sensorManagerObject = new GameObject("SensorManager");

            try
            {
                root.AddComponent<EventSystem>();
                root.AddComponent<StandaloneInputModule>();

                itemContainer.SetParent(panelRoot.transform, false);

                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                cameraObject.transform.position = Vector3.zero;
                cameraObject.transform.rotation = Quaternion.identity;

                target.name = "PlacementTarget";
                target.transform.position = new Vector3(0f, 0f, 5f);
                Physics.SyncTransforms();

                existingSensorObject.AddComponent<TestPlacementSensor>();

                var monitor = panelRoot.AddComponent<MonitorManager>();
                monitor.itemContainer = itemContainer;
                monitor.sensorItemPrefab = sensorItemPrefab;

                var previewSensor = sensorPrefab.AddComponent<TestPlacementSensor>();
                previewSensor.isPreview = true;
                var sensorManager = sensorManagerObject.AddComponent<VsensAgentSensorManager>();
                sensorManager.RegisterSensor(previewSensor);

                var preview = root.AddComponent<SensorPreview>();
                preview.workspaceCanvasGroup = workspaceCanvasGroup;
                preview.sensorPrefab = previewSensor;
                preview.monitorManager = monitor;

                monitor.RefreshSensorList();
                Assert.That(itemContainer.GetComponentsInChildren<SensorUIItem>(true), Has.Length.EqualTo(1));

                preview.BeginPlacement();
                Assert.That(Physics.Raycast(new Ray(Vector3.zero, Vector3.forward), out var hit, 20f), Is.True);

                Assert.That(preview.TryPlaceSensorAtHit(hit), Is.True);

                var sensors = Object.FindObjectsByType<TestPlacementSensor>(FindObjectsSortMode.None)
                    .Where(sensor => sensor != preview.sensorPrefab)
                    .OrderBy(sensor => sensor.name)
                    .ToArray();
                Assert.That(sensors.Select(sensor => sensor.name), Is.EquivalentTo(new[] { "IMU-01", "IMU-02" }));
                Assert.That(sensors.All(sensor => !sensor.isPreview), Is.True);
                Assert.That(itemContainer.GetComponentsInChildren<SensorUIItem>(true), Has.Length.EqualTo(2));
            }
            finally
            {
                foreach (var sensor in Object.FindObjectsByType<TestPlacementSensor>(FindObjectsSortMode.None))
                {
                    if (sensor != null)
                    {
                        Object.DestroyImmediate(sensor.gameObject);
                    }
                }

                Object.DestroyImmediate(sensorItemPrefab);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sensorPrefab);
                Object.DestroyImmediate(sensorManagerObject);
                Object.DestroyImmediate(workspace);
                Object.DestroyImmediate(panelRoot);
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateSensorItemPrefab()
        {
            var root = new GameObject("SensorItemPrefab");
            root.AddComponent<SensorUIItem>();
            return root;
        }

        private class TestPlacementSensor : VirtualSensor
        {
            public override void UpdateWorking(float time, float deltaTime)
            {
            }

            public override ISensorDefinition SensorDefinition()
            {
                return ISensorDefinition.create("IMU", "imu");
            }
        }
    }
}
