using SimpleJSON;

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
        public string phase;
        public ISensorData data;
        
        public string ToCsvLine()
        {
            return $"{sensorID},{time},{phase},{data.ToCsvLine()}";
        }
        
        public JSONNode serialize()
        {
            var json = new JSONObject();
            json["time"] = time;
            json["sensorID"] = sensorID;
            json["phase"] = phase ?? string.Empty;
            json["data"] = data.serialize();
            return json;
        }
        
    }
}
