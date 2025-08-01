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
    
    [Header("Object Properties")]
    [SerializeField] private bool withState = false;
    [SerializeField] private bool movable = false;
    
    public string ObjectName => objectName;

    protected void Awake()
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
    
    public virtual JSONObject GetDescription()
    {
        var data = BoxColliderData.FromBoxCollider(GetOrCreateBoxCollider(), transform).toJSONObject();
        
        // 添加物品属性
        var properties = new JSONArray();
        if (withState)
        {
            properties.Add("with_state");
        }
        if (movable)
        {
            properties.Add("movable");
        }
        data["properties"] = properties;
        
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
    
    /// <summary>
    /// 获取物品的所有属性
    /// </summary>
    public string[] GetProperties()
    {
        var propertiesList = new System.Collections.Generic.List<string>();
        
        if (withState)
            propertiesList.Add("with_state");
        if (movable)
            propertiesList.Add("movable");
            
        return propertiesList.ToArray();
    }
    
    /// <summary>
    /// 检查物品是否具有特定属性
    /// </summary>
    public bool HasProperty(string property)
    {
        switch (property.ToLower())
        {
            case "with_state":
                return withState;
            case "movable":
                return movable;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// 设置物品属性
    /// </summary>
    public void SetProperty(string property, bool value)
    {
        switch (property.ToLower())
        {
            case "with_state":
                withState = value;
                break;
            case "movable":
                movable = value;
                break;
        }
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
[CanEditMultipleObjects]
[CustomEditor(typeof(ObjectDescriber), true)]
public class ObjectDescriberEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ObjectDescriber describer = (ObjectDescriber)target;

        GUILayout.Space(10);
        
        // 显示当前属性信息
        EditorGUILayout.LabelField("Current Properties:", EditorStyles.boldLabel);
        var properties = describer.GetProperties();
        if (properties.Length > 0)
        {
            foreach (var property in properties)
            {
                EditorGUILayout.LabelField("• " + property, EditorStyles.miniLabel);
            }
        }
        else
        {
            EditorGUILayout.LabelField("• No properties selected", EditorStyles.miniLabel);
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("Obtain Object Description"))
        {
            var json = describer.GetDescription();
            describer.jsonOutput = json.ToString(2);
            EditorUtility.SetDirty(describer);
        }
        
        GUILayout.Space(5);
        
        // 批量操作按钮
        if (Selection.gameObjects.Length > 1)
        {
            EditorGUILayout.LabelField($"Batch Operations ({Selection.gameObjects.Length} objects selected):", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Set All Movable"))
            {
                SetPropertyForSelected("movable", true);
            }
            if (GUILayout.Button("Set All With State"))
            {
                SetPropertyForSelected("with_state", true);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All Movable"))
            {
                SetPropertyForSelected("movable", false);
            }
            if (GUILayout.Button("Clear All With State"))
            {
                SetPropertyForSelected("with_state", false);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
    
    private void SetPropertyForSelected(string property, bool value)
    {
        foreach (var obj in Selection.gameObjects)
        {
            var describer = obj.GetComponent<ObjectDescriber>();
            if (describer != null)
            {
                describer.SetProperty(property, value);
                EditorUtility.SetDirty(describer);
            }
        }
    }
}
#endif
