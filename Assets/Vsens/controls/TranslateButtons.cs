using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;

namespace Vsens.controls
{
    public class TranslateButtons : MonoBehaviour
    {
        public VsensPlatform platform;
        public Button xIncrementButton;
        public Button xDecrementButton;
        public Button yIncrementButton;
        public Button yDecrementButton;
        public Button zIncrementButton;
        public Button zDecrementButton;
        public float offset = 0.015f;

        private void Start()
        {
            xIncrementButton.onClick.AddListener(() => platform.UpdateSensorPosition(Axis.X, offset));
            xDecrementButton.onClick.AddListener(() => platform.UpdateSensorPosition(Axis.X, -offset));
            yIncrementButton.onClick.AddListener(() => platform.UpdateSensorPosition(Axis.Y, offset));
            yDecrementButton.onClick.AddListener(() => platform.UpdateSensorPosition(Axis.Y, -offset));
            zIncrementButton.onClick.AddListener(() => platform.UpdateSensorPosition(Axis.Z, offset));
            zDecrementButton.onClick.AddListener(() => platform.UpdateSensorPosition(Axis.Z, -offset));
        }
    }
}
