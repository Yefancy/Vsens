using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VsensAgent.Network.Protocol
{
    /// <summary>
    /// 控制对象 - 描述对场景对象的控制指令
    /// </summary>
    [Serializable]
    public class ControlObject
    {
        public string target;
        public string action;
        
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> parameters;
        
        public ControlObject()
        {
            parameters = new Dictionary<string, object>();
        }
    }
}
