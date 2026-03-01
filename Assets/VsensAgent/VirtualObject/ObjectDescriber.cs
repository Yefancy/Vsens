using System.Collections.Generic;
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
    [SerializeField] private bool movable;
    [SerializeField] private Renderer ObjectRenderer;
    
    public string ObjectName => objectName;

    public string GetObjectName()
    {
        if (!string.IsNullOrEmpty(objectName)) return objectName;
        return gameObject.name;
    }

    public Renderer GetObjectRenderer()
    {
        if (ObjectRenderer != null) return ObjectRenderer;
        ObjectRenderer = GetComponent<Renderer>();
        return ObjectRenderer;
    }
    
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
        foreach (var property in GetProperties())
        {
            properties.Add(property);
        }
        data["properties"] = properties;

        // 添加 transform scale (Phase 2: Python 场景相关性过滤需要完整字段)
        var lossyScale = transform.lossyScale;
        var scaleArr = new JSONArray();
        scaleArr.Add(new JSONNumber((float)System.Math.Round(lossyScale.x, 3)));
        scaleArr.Add(new JSONNumber((float)System.Math.Round(lossyScale.y, 3)));
        scaleArr.Add(new JSONNumber((float)System.Math.Round(lossyScale.z, 3)));
        data["scale"] = scaleArr;

        if (TryGetComponent<IStateHolder>(out var stateHolder))
        {
            data["state"] = stateHolder.getCurrentState();
            var array = new JSONArray();
            foreach (var state in stateHolder.getAvailableStates())
            {
                array.Add(state);
            }
            data["availableStates"] = array;
        }
        return data;
    }
    
    public virtual HashSet<string> GetProperties()
    {
        var propertiesList = new HashSet<string>();
        
        if (TryGetComponent<IStateHolder>(out _))
            propertiesList.Add("with_state");
        if (movable)
            propertiesList.Add("movable");
        
        return propertiesList;
    }
    
    /// <summary>
    /// 检查物品是否具有特定属性
    /// </summary>
    public bool HasProperty(string property)
    {
        return GetProperties().Contains(property);
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
        if (properties.Count > 0)
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
    }
    
}
#endif
