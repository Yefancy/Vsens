using OVRSimpleJSON;

namespace Sensor
{
    public interface ISensorData
    {
        string ToCsvLine();
        JSONNode serialize();
    }
    
    public struct SensorData
    {
        public float time;
        public string sensorID;
        public ISensorData data;
        
        public string ToCsvLine()
        {
            return $"{sensorID},{time},{data.ToCsvLine()}";
        }
        
        public JSONNode serialize()
        {
            var json = new JSONObject();
            json["time"] = time;
            json["sensorID"] = sensorID;
            json["data"] = data.serialize();
            return json;
        }
        
    }
}