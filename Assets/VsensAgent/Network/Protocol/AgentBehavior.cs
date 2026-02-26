using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VsensAgent.Network.Protocol
{
    /// <summary>
    /// Agent行为 - 描述Agent的行为动作
    /// </summary>
    [Serializable]
    public class AgentBehavior
    {
        public string type;
        public string action;
        public string target;
        public string emotion;
        
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> parameters;
        
        public AgentBehavior()
        {
            parameters = new Dictionary<string, object>();
        }
    }
}
