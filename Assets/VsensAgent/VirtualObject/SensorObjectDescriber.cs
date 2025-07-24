using Sensor;
using SimpleJSON;
using UnityEngine;

public class SensorObjectDescriber : ObjectDescriber
{
    [SerializeField] private VirtualSensor _sensor;
    
    protected new void Awake()
    {
        if (_sensor == null)
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
            data["isSensor"] = true;
            data["sensor"] = _sensor.GetSensorDescription();
        }
        return data;
    }
    
}
