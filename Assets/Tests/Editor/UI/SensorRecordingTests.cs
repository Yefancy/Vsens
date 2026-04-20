using System.IO;
using NUnit.Framework;
using Sensor;
using SimpleJSON;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VsensAgent.UI;
using VsensAgent.VirtualObject.Sensor;

namespace VsensAgent.Tests.Editor.UI
{
    public class SensorRecordingTests
    {
        [Test]
        public void StopSensorRecordingAndExport_WritesPerSensorCsvFiles()
        {
            var root = new GameObject("SensorRecordingRoot");
            var exportRoot = Path.Combine(Path.GetTempPath(), $"sensor_recording_test_{System.Guid.NewGuid():N}");

            try
            {
                var dataCenter = root.AddComponent<SensorDataCenter>();
                dataCenter.Start();

                var manager = root.AddComponent<VsensAgentSensorManager>();
                manager.SetExportDirectoryResolver(() => exportRoot);

                var sensorObject = new GameObject("DistanceSensor");
                sensorObject.transform.SetParent(root.transform, false);
                var sensor = sensorObject.AddComponent<TestRecordingSensor>();
                dataCenter.RegisterSensor(sensor);

                Assert.That(manager.StartSensorRecording(), Is.True);
                sensor.EmitSample(0.1f, 1.5f);

                var result = manager.StopSensorRecordingAndExport();

                Assert.That(result.saved, Is.True);
                Assert.That(result.canceled, Is.False);
                Assert.That(result.exportedFileCount, Is.EqualTo(1));

                var files = Directory.GetFiles(result.directoryPath, "*.csv");
                Assert.That(files.Length, Is.EqualTo(1));
                var csv = File.ReadAllText(files[0]);
                Assert.That(csv, Does.Contain("tag,time,distance"));
                Assert.That(csv, Does.Contain("DistanceSensor"));

                Object.DestroyImmediate(sensorObject);
            }
            finally
            {
                if (Directory.Exists(exportRoot))
                {
                    Directory.Delete(exportRoot, true);
                }

                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MonitorManager_RecordingButtonAndTimerReflectRecordingState()
        {
            var root = new GameObject("MonitorRecordingRoot");

            try
            {
                var dataCenter = root.AddComponent<SensorDataCenter>();
                dataCenter.Start();

                var manager = root.AddComponent<VsensAgentSensorManager>();
                manager.SetExportDirectoryResolver(() => string.Empty);

                var monitor = root.AddComponent<MonitorManager>();
                monitor.recordingButton = new GameObject("RecordingButton").AddComponent<Button>();
                monitor.recordingButtonImage = monitor.recordingButton.gameObject.AddComponent<Image>();
                monitor.recordingTimerText = new GameObject("RecordingTimer").AddComponent<TextMeshProUGUI>();

                var startMethod = typeof(MonitorManager).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                startMethod?.Invoke(monitor, null);

                monitor.recordingButton.onClick.Invoke();
                Assert.That(manager.IsRecording, Is.True);
                Assert.That(monitor.recordingButtonImage.color, Is.EqualTo(monitor.recordingButtonRecordingColor));
                Assert.That(monitor.recordingTimerText.gameObject.activeSelf, Is.True);

                monitor.recordingButton.onClick.Invoke();
                Assert.That(manager.IsRecording, Is.False);
                Assert.That(monitor.recordingButtonImage.color, Is.EqualTo(monitor.recordingButtonIdleColor));
                Assert.That(monitor.recordingTimerText.gameObject.activeSelf, Is.False);

                Object.DestroyImmediate(monitor.recordingButton.gameObject);
                Object.DestroyImmediate(monitor.recordingTimerText.gameObject);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyingSensor_UnregistersFromVsensAgentSensorManager()
        {
            var root = new GameObject("SensorManagerRoot");

            try
            {
                var manager = root.AddComponent<VsensAgentSensorManager>();
                var sensorObject = new GameObject("TrackedSensor");
                var sensor = sensorObject.AddComponent<TestRecordingSensor>();

                manager.RegisterSensor(sensor);
                Assert.That(manager.GetRegisteredSensorNames().Count, Is.EqualTo(1));

                Object.DestroyImmediate(sensorObject);

                Assert.That(manager.GetRegisteredSensorNames().Count, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TryRemoveSensor_DestroysSensorGameObject()
        {
            var root = new GameObject("SensorRemoveRoot");

            try
            {
                var manager = root.AddComponent<VsensAgentSensorManager>();
                var sensorObject = new GameObject("DISTANCE-Remove");
                sensorObject.AddComponent<TestRecordingSensor>();

                Assert.That(manager.TryRemoveSensor("DISTANCE-Remove", out var error), Is.True, error);
                Assert.That(sensorObject == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SensorUiDeleteButton_RemovesSensorAndItem()
        {
            var root = new GameObject("SensorUiDeleteRoot");
            var monitorRoot = new GameObject("MonitorRoot");
            var itemContainer = new GameObject("ItemContainer").transform;
            itemContainer.SetParent(monitorRoot.transform, false);
            var sensorItemPrefab = CreateSensorItemPrefab();

            try
            {
                root.AddComponent<SensorDataCenter>().Start();
                root.AddComponent<VsensAgentSensorManager>();

                var sensorObject = new GameObject("DISTANCE-UiDelete");
                sensorObject.AddComponent<TestRecordingSensor>();

                var monitor = monitorRoot.AddComponent<MonitorManager>();
                monitor.itemContainer = itemContainer;
                monitor.sensorItemPrefab = sensorItemPrefab;
                monitor.RefreshSensorList();

                var item = itemContainer.GetComponentInChildren<SensorUIItem>(true);
                Assert.That(item, Is.Not.Null);
                Assert.That(item.deleteButton, Is.Not.Null);

                item.deleteButton.onClick.Invoke();

                Assert.That(sensorObject == null, Is.True);
                Assert.That(itemContainer.GetComponentInChildren<SensorUIItem>(true), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sensorItemPrefab);
                Object.DestroyImmediate(monitorRoot);
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateSensorItemPrefab()
        {
            var root = new GameObject("SensorItemPrefab");
            root.AddComponent<SensorUIItem>();
            return root;
        }

        private class TestRecordingSensor : VirtualSensor
        {
            public void EmitSample(float time, float distance)
            {
                AppendData(time, new TestDistanceData(distance));
            }

            public override void UpdateWorking(float time, float deltaTime)
            {
            }

            public override ISensorDefinition SensorDefinition()
            {
                return ISensorDefinition.create("DISTANCE", "distance");
            }
        }

        private readonly struct TestDistanceData : ISensorData
        {
            private readonly float _distance;

            public TestDistanceData(float distance)
            {
                _distance = distance;
            }

            public string ToCsvLine()
            {
                return _distance.ToString("F3");
            }

            public JSONNode serialize()
            {
                return new JSONNumber(_distance);
            }
        }
    }
}
