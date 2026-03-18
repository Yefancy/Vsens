using System;
using System.Collections.Generic;

namespace VsensAgent.SceneApi.V2
{
    [Serializable]
    public class SceneSnapshot
    {
        public string scene_id;
        public int scene_version;
        public string generated_at;
        public List<SceneObjectModel> objects = new List<SceneObjectModel>();
        public List<SceneSurfaceModel> surfaces = new List<SceneSurfaceModel>();
        public List<SceneZoneModel> zones = new List<SceneZoneModel>();
        public List<SceneRelationModel> relations = new List<SceneRelationModel>();
    }

    [Serializable]
    public class SceneObjectModel
    {
        public string id;
        public string alias;
        public string kind;
        public string room_id;
        public Vector3Data position;
        public Vector3Data rotation;
        public Vector3Data scale;
        public Vector3Data bounds_center;
        public Vector3Data bounds_size;
        public Dictionary<string, object> state = new Dictionary<string, object>();
        public List<string> capabilities = new List<string>();
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class SceneSurfaceModel
    {
        public string surface_id;
        public string parent_object_id;
        public string surface_type;
        public Vector3Data center;
        public Vector3Data normal;
        public List<Vector3Data> boundary_polygon = new List<Vector3Data>();
        public bool mountable;
        public float clearance_min;
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class SceneZoneModel
    {
        public string zone_id;
        public string name;
        public string type;
        public Vector3Data center;
        public Vector3Data size;
    }

    [Serializable]
    public class SceneRelationModel
    {
        public string type;
        public string from_id;
        public string to_id;
        public Dictionary<string, object> metadata = new Dictionary<string, object>();
    }

    [Serializable]
    public class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data() {}

        public Vector3Data(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    [Serializable]
    public class PlacementCandidate
    {
        public string candidate_id;
        public string surface_id;
        public Vector3Data position;
        public Vector3Data rotation;
        public float score;
        public float coverage_score;
        public float occlusion_risk;
        public float distance_penalty;
        public bool collision_free;
        public List<string> explanations = new List<string>();
    }

    [Serializable]
    public class AvatarQueryModel
    {
        public string avatar_id;
        public string object_id;
        public string prefab_key;
        public Vector3Data position;
        public Vector3Data rotation;
        public string motion_id;
        public string motion_name;
        public bool is_playing;
    }

    [Serializable]
    public class AvatarMotionQueryModel
    {
        public string motion_id;
        public string motion_name;
        public string source_text;
        public bool has_inline_json;
        public string loaded_to_avatar_id;
    }

    [Serializable]
    public class ActionBatchRequestV2
    {
        public string request_id;
        public int scene_version;
        public bool strict = true;
        public List<ActionCommandV2> actions = new List<ActionCommandV2>();
    }

    [Serializable]
    public class ActionCommandV2
    {
        public string target_id;
        public string action_type;
        public Dictionary<string, object> parameters = new Dictionary<string, object>();
        public List<string> preconditions = new List<string>();
    }

    [Serializable]
    public class ActionErrorItemV2
    {
        public string code;
        public string message;
        public string target_id;
        public int action_index;
        public string hint;
    }

    [Serializable]
    public class ValidationReportV2
    {
        public string request_id;
        public int scene_version;
        public bool ok;
        public List<ActionErrorItemV2> errors = new List<ActionErrorItemV2>();
        public List<ActionCommandV2> normalized_actions = new List<ActionCommandV2>();
    }

    [Serializable]
    public class ExecutionReportV2
    {
        public string request_id;
        public string status;
        public int scene_version_before;
        public int scene_version_after;
        public List<int> applied_actions = new List<int>();
        public int failed_action_index = -1;
        public string rollback_status;
        public long duration_ms;
        public List<ActionErrorItemV2> errors = new List<ActionErrorItemV2>();
        public string trace_id;
    }

    public static class SceneApiErrorCodes
    {
        public const string TARGET_NOT_FOUND = "TARGET_NOT_FOUND";
        public const string UNSUPPORTED_ACTION = "UNSUPPORTED_ACTION";
        public const string INVALID_PARAM = "INVALID_PARAM";
        public const string SCENE_VERSION_MISMATCH = "SCENE_VERSION_MISMATCH";
        public const string CONSTRAINT_VIOLATION = "CONSTRAINT_VIOLATION";
        public const string PLACEMENT_NOT_MOUNTABLE = "PLACEMENT_NOT_MOUNTABLE";
        public const string PLACEMENT_COLLISION = "PLACEMENT_COLLISION";
        public const string COVERAGE_INSUFFICIENT = "COVERAGE_INSUFFICIENT";
        public const string TRANSACTION_ROLLBACK_FAILED = "TRANSACTION_ROLLBACK_FAILED";
    }
}
