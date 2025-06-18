using SimpleJSON;
using UnityEditor;
using UnityEngine;

public class RoomDescriber : MonoBehaviour
{
    [TextArea(5, 20)] // Inspector 中多行显示
    public string jsonOutput;

    public JSONObject ExportBoxColliderAsJson()
    {
        var objects = new JSONObject();
        foreach (var objectDescriber in GetComponentsInChildren<ObjectDescriber>())
        {
            var data = objectDescriber.ExportBoxColliderAsJson();
            objects.Add(objectDescriber.name, data);
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
        if (GUILayout.Button("导出房间描述 为 JSON"))
        {
            var json = describer.ExportBoxColliderAsJson();
            describer.jsonOutput = json.ToString(2);
            EditorUtility.SetDirty(describer);
        }
    }
}
#endif
