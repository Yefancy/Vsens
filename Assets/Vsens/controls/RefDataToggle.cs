using System;
using System.Collections.Generic;
using Animations;

using Sensor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vsens.controls
{
    [RequireComponent(typeof(Toggle))]
    public class RefDataToggle : MonoBehaviour
    {
        public TextMeshProUGUI label;
        public List<SensorData> refData;
        private Toggle _toggle;
        
        private void Awake()
        {
            _toggle = GetComponent<Toggle>();
        }
        
        public void SetRefData(String name, List<SensorData> data)
        {
            refData = data;
            if (label != null)
            {
                label.text = name;
            }
        }
    }
}