using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using SimpleJSON;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

namespace Sensor
{
    public abstract class VirtualSensor : MonoBehaviour
    {
        [Tooltip("to visualize the object when it is in active.")]
        [SerializeField] [AllowNull] public GameObject inActiveVisualization;
        [Tooltip("to visualize the object when it is in active.")]
        [SerializeField] [AllowNull] public GameObject preview;
        [Tooltip("to visualize the object when it is selected.")]
        [SerializeField] [AllowNull] public GameObject selectedVisualization;
        [Tooltip("to visualize the object when it is selected.")]
        [SerializeField] [AllowNull] public GameObject visualBox;
        [FormerlySerializedAs("graphController")]
        [Tooltip("to visualize the Graph Chart.")]
        [SerializeField] [AllowNull] public GameObject graphChart;
        [Tooltip("to visualize the object when it is selected.")]
        [SerializeField] [AllowNull] public new Rigidbody rigidbody;
        [Tooltip("duration for transform mode. grabbing time less than the duration, the sensor will be in selecting mode.")]
        public float modeSwitchTime = 1.0f;
        [Tooltip("jitter duration. the sensor will jitter for 1 second to notify the user that the sensor is in transform mode.")]
        public float jitterTime = 0.2f;
        [Tooltip("The hand condition that the sensor can be controlled by.")]
        public SensorAttachable.HandCondition controlledHand = SensorAttachable.HandCondition.Both;
        [Tooltip("prefab of the sensor.")]
        public GameObject prefab;
        public bool showSelectedVisualization = true;
        protected SensorDataCenter sensorDataCenter => SensorDataCenter.Instance;
        public Rigidbody Rigidbody => rigidbody;
        public bool registerOnStart = false;
        public bool canBeTransform = true;
        public bool canSelected = true;
        public bool interactable = true;
        public bool canDeselect = true;
        public bool isPreview = false;

        /// <summary>
        /// to check if the sensor is working, if ture the sensor will collect data, otherwise it wont.
        /// </summary>
        private bool isActive = true;
        /// <summary>
        /// Sensor is active or not. if it is not active, it will not collect data.
        /// </summary>
        public bool IsActive
        {
            get => isActive;
            set
            {
                if (IsActive == value) return;
                isActive = value;
                inActiveVisualization?.SetActive(!value);
                onActiveChanged?.Invoke(value);
            }
        }
        public Action<bool> onActiveChanged;
        
        
        /// <summary>
        /// to show the preview of the sensor.
        /// </summary>
        private bool showPreview;
        public bool ShowPreview
        {
            get => showPreview;
            set
            {
                if (ShowPreview == value) return;
                showPreview = value;
                preview?.SetActive(value);
                onShowPreviewChanged?.Invoke(value);
            }
        }

        public Action<bool> onShowPreviewChanged;
        
        /// <summary>
        /// to show the graph of the sensor.
        /// </summary>
        private bool showGraph;
        public bool ShowGraph
        {
            get => showGraph;
            set
            {
                if (ShowGraph == value) return;
                showGraph = value;
                graphChart?.SetActive(value);
                onShowGraphChanged?.Invoke(value);
            }
        }

        public Action<bool> onShowGraphChanged;

        /// <summary>
        /// Only one sensor can be selected at a time.
        /// </summary>
        private static VirtualSensor _SelectedSensor;
        public static VirtualSensor SelectedSensor => _SelectedSensor;
        public bool isSelected
        {
            get => _SelectedSensor == this;
            set
            {
                if (value)
                {
                    if (_SelectedSensor != null)
                    {
                        _SelectedSensor.isSelected = false;
                    }
                    onSelectedChanged?.Invoke(true);
                    if (showSelectedVisualization)
                    {
                        selectedVisualization?.SetActive(true);
                    }
                    visualBox?.SetActive(true);
                    _SelectedSensor = this;
                }
                else if (_SelectedSensor == this)
                {
                    onSelectedChanged?.Invoke(false);
                    selectedVisualization?.SetActive(false);
                    visualBox?.SetActive(false);
                    _SelectedSensor = null;
                }
            }
        }
        public Action<bool> onSelectedChanged;

        protected virtual void Start()
        {
            if (registerOnStart)
            {
                StartCoroutine(Register());
            }

            if (sensorAttachable == null)
            {
                sensorAttachable = GetComponent<SensorAttachable>();
                if (sensorAttachable == null)
                {
                    sensorAttachable = GetComponentInParent<SensorAttachable>();
                }
            }

            if (sensorAttachable != null)
            {
                sensorAttachable.OnAttachTo(this);
            }
        }
        
        private IEnumerator Register()
        {
            yield return null;
            sensorDataCenter.RegisterSensor(this);
        }

        
        
        // run-time
        /// <summary>
        /// it will be used to record the time when the sensor is selected.
        /// -1 means the sensor is not selected.
        /// >=0 means the sensor is selecting.
        /// if selectedTime bigger than durationForTransformMode, the sensor will be in transform mode,
        /// else it will be in selecting mode.
        /// </summary>
        private float selectedTime = -1;
        private SensorPlacement sensorPlacement;
        private Vector3 lastPosition;
        private readonly List<SensorData> _sensorData = new();
        public List<SensorData> Data => _sensorData;
        private bool isRecording;
        public SensorAttachable sensorAttachable;
        public event Action<SensorData, bool> onDataAppended;

        public virtual void OnAttachTo(SensorAttachable attachable)
        {
            Detach();
            sensorAttachable = attachable;
        }
        
        public virtual void Detach()
        {
            if (sensorAttachable != null)
            {
                sensorAttachable.OnSensorDetach(this);
                sensorAttachable = null;
            }
        }
        
        protected void AppendData(float time, ISensorData data)
        {
            var sample = new SensorData
            {
                time = time,
                sensorID = name,
                data = data
            };

            if (isRecording)
            {
                _sensorData.Add(sample);
            }

            onDataAppended?.Invoke(sample, isRecording);
        }
    
        public virtual void ClearData()
        {
            _sensorData.Clear();
        }

        /// <summary>
        /// it will be called if and only if the sensor is working.
        /// </summary>
        public abstract void UpdateWorking(float time, float deltaTime);

        /// <summary>
        /// sensors with the same definition will be saved in the same file.
        /// </summary>
        public abstract ISensorDefinition SensorDefinition();

        void Update()
        {
            // sensor is selected
            if (selectedTime >= 0)
            {
                selectedTime += Time.deltaTime;
                if (selectedTime >= modeSwitchTime && canBeTransform)
                {
                    // transform mode
                    if (sensorPlacement == null)
                    { // create sensor placement for the first time.
                        sensorPlacement = Rigidbody.AddComponent<SensorPlacement>();
                    }
                    isSelected = false;
                    if (selectedTime <= modeSwitchTime + jitterTime)
                    {
                        // jitter 1 second to notify the user that the sensor is in transform mode.
                        var value = (modeSwitchTime + jitterTime - selectedTime) / jitterTime;
                        transform.position = lastPosition + Vector3.up * (0.007f * Mathf.Sin(value * Mathf.PI * 8));
                    }
                }
            }
        }
    
        void FixedUpdate()
        {
            if (IsActive)
            {
                UpdateWorking(Time.fixedTime, Time.fixedDeltaTime);
            }
        }
    
        public void StartRecording()
        {
            isRecording = true;
        }
    
        public void StopRecording()
        {
            isRecording = false;
        }

        public virtual void ClearSmoothCache()
        {
        
        }

        private void OnDestroy()
        {
            SensorDataCenter.Instance?.UnregisterSensor(this);
            if (graphChart != null) Destroy(graphChart);
            Detach();
        }
        
        public virtual JSONObject GetSensorDescription()
        {
            var description = new JSONObject();
            var definition = SensorDefinition();
            description["sensorType"] = definition.getSensorName();
            description["showVisualization"] = showPreview;
            description["showDataGraph"] = showGraph;
            return description;
        }
    }
}
