using NUnit.Framework;
using OVRSimpleJSON;
using Sensor;
using UnityEngine;
using UnityEngine.UI;
using VsensAgent.UI;
using XCharts.Runtime;

public class SensorRealtimeDataTests
{
    [Test]
    public void AppendData_EmitsRealtimeEvent_EvenWhenNotRecording()
    {
        var go = new GameObject("TestSensor");
        try
        {
            var sensor = go.AddComponent<TestRealtimeSensor>();
            SensorData? lastSample = null;
            bool? lastRecordingState = null;

            sensor.onDataAppended += (sample, isRecording) =>
            {
                lastSample = sample;
                lastRecordingState = isRecording;
            };

            sensor.EmitSample(1.25f, 42f);

            Assert.That(lastSample.HasValue, Is.True);
            Assert.That(lastRecordingState.HasValue, Is.True);
            Assert.That(lastRecordingState.Value, Is.False);
            Assert.That(lastSample.Value.sensorID, Is.EqualTo("TestSensor"));
            Assert.That(lastSample.Value.time, Is.EqualTo(1.25f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SensorUIItem_ExpandedChartMatchesImuDataDimension()
    {
        var sensorGo = new GameObject("ImuSensor");
        var itemGo = new GameObject("SensorItem");
        try
        {
            var sensor = sensorGo.AddComponent<TestRealtimeImuSensor>();
            var lineChart = itemGo.AddComponent<LineChart>();
            var detailContainer = new GameObject("DetailContainer");
            detailContainer.transform.SetParent(itemGo.transform, false);
            detailContainer.SetActive(true);
            var toggleGo = new GameObject("DetailToggle");
            toggleGo.transform.SetParent(itemGo.transform, false);
            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.isOn = true;

            var item = itemGo.AddComponent<SensorUIItem>();
            item.chart = lineChart;
            item.detailContainer = detailContainer;
            item.detailToggle = toggle;
            item.Initialize(sensor);

            sensor.EmitImuSample(0.5f, new Vector3(1f, 2f, 3f));

            Assert.That(lineChart.series.Count, Is.EqualTo(3));
        }
        finally
        {
            Object.DestroyImmediate(itemGo);
            Object.DestroyImmediate(sensorGo);
        }
    }

    private class TestRealtimeSensor : VirtualSensor
    {
        public void EmitSample(float time, float value)
        {
            AppendData(time, new TestRealtimeData { value = value });
        }

        public override void UpdateWorking(float time, float deltaTime)
        {
        }

        public override ISensorDefinition SensorDefinition()
        {
            return ISensorDefinition.create("TEST", "value");
        }

        private struct TestRealtimeData : ISensorData
        {
            public float value;

            public string ToCsvLine()
            {
                return value.ToString();
            }

            public JSONNode serialize()
            {
                var json = new JSONObject();
                json["value"] = value;
                return json;
            }
        }
    }

    private class TestRealtimeImuSensor : VirtualIMUSensor
    {
        public void EmitImuSample(float time, Vector3 localAcceleration)
        {
            AppendData(time, new TestImuRealtimeData { localAcceleration = localAcceleration });
        }

        private struct TestImuRealtimeData : ISensorData
        {
            public Vector3 localAcceleration;

            public string ToCsvLine()
            {
                return $"{localAcceleration.x},{localAcceleration.y},{localAcceleration.z}";
            }

            public JSONNode serialize()
            {
                var json = new JSONObject();
                json["localAcceleration"] = Utils.SerializeVector3(localAcceleration);
                return json;
            }
        }
    }
}
