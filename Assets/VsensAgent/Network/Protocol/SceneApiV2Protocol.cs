using System;
using System.Collections.Generic;

namespace VsensAgent.Network.Protocol
{
    [Serializable]
    public class SceneApiRequestEnvelope
    {
        public string type;
        public string request_id;
        public int scene_version;
    }

    [Serializable]
    public class SceneApiResponseEnvelope
    {
        public string type;
        public string request_id;
        public int scene_version;
        public string code;
        public string message;
    }

    [Serializable]
    public class SceneQueryObjectsRequest : SceneApiRequestEnvelope
    {
        public List<string> ids = new List<string>();
        public List<string> aliases = new List<string>();
        public List<string> tags = new List<string>();
        public string zone_id;
    }

    [Serializable]
    public class SceneQueryRelationsRequest : SceneApiRequestEnvelope
    {
        public string object_id;
        public List<string> relation_types = new List<string>();
        public float radius;
    }

    [Serializable]
    public class SceneQuerySurfacesRequest : SceneApiRequestEnvelope
    {
        public string near_object_id;
        public string zone_id;
        public bool mountable_only = true;
    }
}
