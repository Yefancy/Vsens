using UnityEngine;
using UnityEditor;
using System.Linq;

#if UNITY_EDITOR
/// <summary>
/// 批量设置ObjectDescriber属性的工具
/// </summary>
public class ObjectPropertiesBatchTool : EditorWindow
{
    private bool setWithState = false;
    private bool setMovable = false;
    private bool applyToChildren = true;
    
    [MenuItem("Tools/Object Properties Batch Tool")]
    public static void ShowWindow()
    {
        GetWindow<ObjectPropertiesBatchTool>("Object Properties Tool");
    }
    
    void OnGUI()
    {
        GUILayout.Label("Batch Object Properties Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        // 选择要设置的属性
        EditorGUILayout.LabelField("Properties to Set:", EditorStyles.boldLabel);
        setWithState = EditorGUILayout.Toggle("With State", setWithState);
        setMovable = EditorGUILayout.Toggle("Movable", setMovable);
        
        GUILayout.Space(10);
        
        // 选项
        applyToChildren = EditorGUILayout.Toggle("Apply to Children", applyToChildren);
        
        GUILayout.Space(10);
        
        // 显示当前选择的对象数量
        var selectedObjects = Selection.gameObjects;
        EditorGUILayout.LabelField($"Selected Objects: {selectedObjects.Length}");
        
        if (selectedObjects.Length == 0)
        {
            EditorGUILayout.HelpBox("Please select one or more GameObjects in the scene.", MessageType.Warning);
            return;
        }
        
        GUILayout.Space(10);
        
        // 应用按钮
        if (GUILayout.Button("Apply Properties to Selected Objects"))
        {
            ApplyPropertiesToSelected();
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("Clear All Properties from Selected Objects"))
        {
            ClearPropertiesFromSelected();
        }
        
        GUILayout.Space(20);
        
        // 统计信息
        EditorGUILayout.LabelField("Statistics:", EditorStyles.boldLabel);
        var allDescribers = FindObjectsOfType<ObjectDescriber>();
        var withStateCount = allDescribers.Count(d => d.HasProperty("with_state"));
        var movableCount = allDescribers.Count(d => d.HasProperty("movable"));
        
        EditorGUILayout.LabelField($"Total Objects with Describers: {allDescribers.Length}");
        EditorGUILayout.LabelField($"Objects with 'with_state': {withStateCount}");
        EditorGUILayout.LabelField($"Objects with 'movable': {movableCount}");
    }
    
    private void ApplyPropertiesToSelected()
    {
        var selectedObjects = Selection.gameObjects;
        int processedCount = 0;
        
        foreach (var obj in selectedObjects)
        {
            var targets = applyToChildren ? 
                obj.GetComponentsInChildren<ObjectDescriber>() : 
                new[] { obj.GetComponent<ObjectDescriber>() }.Where(d => d != null).ToArray();
                
            foreach (var describer in targets)
            {
                if (describer != null)
                {
                    describer.SetProperty("with_state", setWithState);
                    describer.SetProperty("movable", setMovable);
                    EditorUtility.SetDirty(describer);
                    processedCount++;
                }
            }
        }
        
        Debug.Log($"[ObjectPropertiesBatchTool] Applied properties to {processedCount} ObjectDescriber components.");
    }
    
    private void ClearPropertiesFromSelected()
    {
        var selectedObjects = Selection.gameObjects;
        int processedCount = 0;
        
        foreach (var obj in selectedObjects)
        {
            var targets = applyToChildren ? 
                obj.GetComponentsInChildren<ObjectDescriber>() : 
                new[] { obj.GetComponent<ObjectDescriber>() }.Where(d => d != null).ToArray();
                
            foreach (var describer in targets)
            {
                if (describer != null)
                {
                    describer.SetProperty("with_state", false);
                    describer.SetProperty("movable", false);
                    EditorUtility.SetDirty(describer);
                    processedCount++;
                }
            }
        }
        
        Debug.Log($"[ObjectPropertiesBatchTool] Cleared properties from {processedCount} ObjectDescriber components.");
    }
}
#endif
