using SimpleJSON;
using UnityEditor;
using UnityEngine;

public class RoomDescriber : MonoBehaviour
{
    [TextArea(5, 20)] // Inspector 中多行显示
    public string jsonOutput;

    public JSONObject GetRoomDescription()
    {
        var objects = new JSONObject();
        foreach (var objectDescriber in GetComponentsInChildren<ObjectDescriber>())
        {
            var data = objectDescriber.GetDescription();
            var objectName = objectDescriber.ObjectName;
            if (objectName.Length == 0)
            {
                objectName = objectDescriber.name;
            }
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
