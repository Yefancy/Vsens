using SimpleJSON;
using UnityEditor;
using UnityEngine;
using VsensAgent;

/// <summary>
/// 扩展版本的ObjectDescriber，展示如何添加更多属性
/// 如果你需要更多属性，可以参考这个实现
/// </summary>
public class ExtendedObjectDescriber : ObjectDescriber
{
    [Header("Extended Properties")]
    [SerializeField] private bool interactable = false;
    [SerializeField] private bool fragile = false;
    [SerializeField] private bool heavy = false;
    [SerializeField] private bool transparent = false;
    
    public override JSONObject GetDescription()
    {
        // 先获取基础描述
        var data = base.GetDescription();
        
        // 添加扩展属性到现有的properties数组
        var properties = data["properties"].AsArray;
        
        if (interactable)
            properties.Add("interactable");
        if (fragile)
            properties.Add("fragile");
        if (heavy)
            properties.Add("heavy");
        if (transparent)
            properties.Add("transparent");
            
        return data;
    }
    
    /// <summary>
    /// 扩展属性检查
    /// </summary>
    public new bool HasProperty(string property)
    {
        // 先检查基础属性
        if (base.HasProperty(property))
            return true;
            
        // 检查扩展属性
        switch (property.ToLower())
        {
            case "interactable":
                return interactable;
            case "fragile":
                return fragile;
            case "heavy":
                return heavy;
            case "transparent":
                return transparent;
            default:
                return false;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ExtendedObjectDescriber))]
public class ExtendedObjectDescriberEditor : ObjectDescriberEditor
{
    public override void OnInspectorGUI()
    {
        // 绘制基础Inspector
        base.OnInspectorGUI();
        
        // 添加扩展功能的说明
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Extended Object Describer: Includes additional properties like interactable, fragile, heavy, and transparent.", MessageType.Info);
    }
}
#endif
