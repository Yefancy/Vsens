using SimpleJSON;
using UnityEditor;
using UnityEngine;
using VsensAgent;

public class ObjectDescriber : MonoBehaviour
{
    [SerializeField] private BoxCollider _boxCollider;
    [SerializeField] private string objectName;
    [TextArea(5, 20)]
    public string jsonOutput;
    
    public string ObjectName => objectName;

    private void Awake()
    {
        _boxCollider = GetOrCreateBoxCollider();
    }
    
    private BoxCollider GetOrCreateBoxCollider()
    {
        if (_boxCollider != null) return _boxCollider;
        _boxCollider = GetComponent<BoxCollider>();
        if (_boxCollider ==null)
        {
            _boxCollider = gameObject.AddComponent<BoxCollider>();
        }
        return _boxCollider;
    }
    
    public JSONObject GetDescription()
    {
        var data = BoxColliderData.FromBoxCollider(GetOrCreateBoxCollider(), transform).toJSONObject();
        var stateHolder = GetComponent<IStateHolder>();
        if (stateHolder == null) return data;
        data["state"] = stateHolder.getCurrentState();
        var array = new JSONArray();
        foreach (var state in stateHolder.getAvailableStates())
        {
            array.Add(state);
        }
        data["availableStates"] = array;
        return data;
    }
    
    private Vector3 RoundVector3(Vector3 v, int digits = 3)
    {
        return new Vector3(
            (float)System.Math.Round(v.x, digits),
            (float)System.Math.Round(v.y, digits),
            (float)System.Math.Round(v.z, digits)
        );
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ObjectDescriber))]
public class ObjectDescriberEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ObjectDescriber describer = (ObjectDescriber)target;

        GUILayout.Space(10);
        if (GUILayout.Button("Obtain Object Description"))
        {
            var json = describer.GetDescription();
            describer.jsonOutput = json.ToString(2);
            EditorUtility.SetDirty(describer);
        }
    }
}
#endif
