using SimpleJSON;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class RoomDescriber : MonoBehaviour
{
    [SerializeField]
    public List<ObjectDescriber> additionalDescribers = new List<ObjectDescriber>();

    [Tooltip("Camera whose world-space position is reported as the 'user' entry in every scene snapshot. " +
             "Leave blank to fall back to Camera.main.")]
    [SerializeField]
    private Camera userCamera;

    [TextArea(5, 20)] // Inspector 中多行显示
    public string jsonOutput;

    public JSONObject GetRoomDescription()
    {
        var objects = new JSONObject();
        foreach (var objectDescriber in GetComponentsInChildren<ObjectDescriber>())
        {
            if (objectDescriber == null || !objectDescriber.isActiveAndEnabled)
                continue;
            var data = objectDescriber.GetDescription();
            var objectName = objectDescriber.GetObjectName();
            objects.Add(objectName, data);
        }

        foreach (var additionalDescriber in additionalDescribers)
        {
            if (additionalDescriber == null)
            {
                Debug.LogWarning("[RoomDescriber] additionalDescribers list contains a missing (null) entry — skipping. Remove the broken reference in the Inspector.");
                continue;
            }
            var data = additionalDescriber.GetDescription();
            var objectName = additionalDescriber.GetObjectName();
            objects.Add(objectName, data);
        }

        // Phase 3 — include head-tracked user position for USER_PROXIMITY events.
        // Python's SceneDiff fires USER_PROXIMITY when the user moves > 0.5 m per heartbeat.
        var cam = userCamera != null ? userCamera : Camera.main;
        if (cam != null)
        {
            var pos = cam.transform.position;
            var userNode = new JSONObject();
            var posArray = new JSONArray();
            posArray.Add(pos.x);
            posArray.Add(pos.y);
            posArray.Add(pos.z);
            userNode.Add("position", posArray);
            userNode.Add("properties", new JSONArray());
            objects.Add("user", userNode);
        }

        return objects;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RoomDescriber))]
public class RoomDescriberEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RoomDescriber describer = (RoomDescriber)target;

        GUILayout.Space(10);
        if (GUILayout.Button("Obtain Room Description"))
        {
            var json = describer.GetRoomDescription();
            describer.jsonOutput = json.ToString(2);
            EditorUtility.SetDirty(describer);
        }
    }
}
#endif
