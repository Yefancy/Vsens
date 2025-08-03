using System.Collections.Generic;
using Sensor;
using SimpleJSON;
using UnityEngine;

public class SensorObjectDescriber : ObjectDescriber
{
    [SerializeField] private VirtualSensor _sensor;
    
    protected new void Awake()
    {
        if (_sensor ==null)
        {
            _sensor = GetComponent<VirtualSensor>();
        }

        base.Awake();
    }
    
    public override JSONObject GetDescription()
    {
        var data = base.GetDescription();
        if (_sensor != null)
        {
            data["sensor"] = _sensor.GetSensorDescription();
            var parent = _sensor.transform.parent;
            if (parent != null && parent.TryGetComponent(out ObjectDescriber parentDescriber))
            {
                data["attachTo"] = parentDescriber.GetObjectName();
            }
        }
        return data;
    }

    public override HashSet<string> GetProperties()
    {
        var properties = base.GetProperties();
        properties.Add("sensor");
        return properties;
    }
}
