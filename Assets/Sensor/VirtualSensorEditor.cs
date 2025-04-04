using UnityEditor;
using UnityEngine;

namespace Sensor
{
    [CustomEditor(typeof(VirtualSensor), true)]
    public class VirtualSensorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var sensor = (VirtualSensor)target;
            DrawDefaultInspector();
            sensor.IsActive = GUILayout.Toggle(sensor.IsActive, "Active");
            sensor.ShowPreview = GUILayout.Toggle(sensor.ShowPreview, "Show Preview");
            sensor.ShowGraph = GUILayout.Toggle(sensor.ShowGraph, "Show Graph");
        }
    }
}