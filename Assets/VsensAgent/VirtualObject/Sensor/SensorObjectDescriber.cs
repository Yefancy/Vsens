using System.Collections.Generic;
using Sensor;
using SimpleJSON;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.VirtualObject.Sensor;

public class SensorObjectDescriber : ObjectDescriber
{
    [SerializeField] private VirtualSensor _sensor;

    public VirtualSensor Sesnor => _sensor;
    private bool IsInit;
    public bool IsInited => IsInit;
    
    protected new void Awake()
    {
        if (_sensor == null)
        {
            _sensor = GetComponent<VirtualSensor>();
        }

        base.Awake();
    }

    private void Start()
    {
        // 设置传感器的唯一名称
        if (!IsInited && _sensor != null)
        {
            SetupSensorName();
        }
    }

    /// <summary>
    /// 为传感器设置唯一的名称：传感器类型-序号
    /// </summary>
    private void SetupSensorName()
    {
        var sensorManager = VsensAgentSensorManager.Instance
            ?? (ServiceLocator.IsRegistered<VsensAgentSensorManager>()
                ? ServiceLocator.Get<VsensAgentSensorManager>()
                : FindFirstObjectByType<VsensAgentSensorManager>());
        if (sensorManager != null)
        {
            sensorManager.EnsureSensorObjectName(_sensor);
            return;
        }

        SyncObjectNameFromSensor();
    }

    public void SyncObjectNameFromSensor()
    {
        if (_sensor == null)
        {
            _sensor = GetComponent<VirtualSensor>();
        }

        if (_sensor == null)
        {
            return;
        }

        SetObjectName(gameObject.name);
        IsInit = true;
    }

    private void SetObjectName(string sensorName)
    {
        var objectNameField = typeof(ObjectDescriber).GetField("objectName", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (objectNameField != null)
        {
            objectNameField.SetValue(this, sensorName);
        }
    }
    
    public override JSONObject GetDescription()
    {
        var data = base.GetDescription();
        if (_sensor != null)
        {
            var parent = _sensor.transform.parent;
            if (parent != null)
            {
                var parentDescriber = parent.GetComponentInParent<ObjectDescriber>();
                if (parentDescriber != null)
                {
                    data["attachTo"] = parentDescriber.GetObjectName();
                }
            }
            data["sensor"] = _sensor.GetSensorDescription();
        }
        return data;
    }

    public override HashSet<string> GetProperties()
    {
        var properties = base.GetProperties();
        
        properties.Add("sensor");
        properties.Add("movable");
        
        return properties;
    }
}
