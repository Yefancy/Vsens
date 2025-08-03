using SimpleJSON;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class RoomDescriber : MonoBehaviour
{
    [SerializeField]
    public List<ObjectDescriber> additionalDescribers = new List<ObjectDescriber>();
    [TextArea(5, 20)] // Inspector 中多行显示
    public string jsonOutput;

    public JSONObject GetRoomDescription()
    {
        var objects = new JSONObject();
        foreach (var objectDescriber in GetComponentsInChildren<ObjectDescriber>())
        {
            var data = objectDescriber.GetDescription();
            var objectName = objectDescriber.GetObjectName();
            objects.Add(objectName, data);
        }

        foreach (var additionalDescriber in additionalDescribers)
        {
            var data = additionalDescriber.GetDescription();
            var objectName = additionalDescriber.GetObjectName();
            objects.Add(objectName, data);
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
