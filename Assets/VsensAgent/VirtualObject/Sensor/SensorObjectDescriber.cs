using System.Collections.Generic;
using Sensor;
using SimpleJSON;
using UnityEngine;
using System;

public class SensorObjectDescriber : ObjectDescriber
{
    [SerializeField] private VirtualSensor _sensor;
    
    // 用于生成唯一序号的静态计数器
    private static Dictionary<string, int> sensorCounters = new Dictionary<string, int>();
    
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
        string sensorType = _sensor.SensorDefinition().getSensorName();
        if (HasExplicitSensorName(gameObject.name, sensorType))
        {
            SetObjectName(gameObject.name);
            Debug.Log($"[SensorObjectDescriber] Preserved sensor name: {gameObject.name}");
            IsInit = true;
            return;
        }
        
        // 获取并递增该类型传感器的计数器
        if (!sensorCounters.ContainsKey(sensorType))
        {
            sensorCounters[sensorType] = 0;
        }
        sensorCounters[sensorType]++;
        
        // 设置objectName为"传感器类型-序号"格式
        string sensorName = $"{sensorType}-{sensorCounters[sensorType]:00}";
        
        SetObjectName(sensorName);
        
        // 同时设置GameObject的名称
        gameObject.name = sensorName;
        
        Debug.Log($"[SensorObjectDescriber] Set sensor name to: {sensorName}");
        IsInit = true;
    }

    private static bool HasExplicitSensorName(string candidate, string sensorType)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (candidate.Contains("(Clone)") || candidate.EndsWith("Prefab", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.Equals(candidate, sensorType, StringComparison.OrdinalIgnoreCase);
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
