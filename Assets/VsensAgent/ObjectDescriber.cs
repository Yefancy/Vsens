using SimpleJSON;
using UnityEditor;
using UnityEngine;

public class ObjectDescriber : MonoBehaviour
{
    [System.Serializable]
    public class BoxColliderData
    {
        public Vector3 position;   // 世界坐标
        public Vector3 size;       // 世界空间下 size
        public Vector3 rotation;   // 欧拉角形式的旋转
        
        public override string ToString()
        {
            return $"Position: {position}, Size: {size}, Rotation: {rotation}";
        }

        public JSONObject toJSONObject()
        {
            JSONObject json = new JSONObject();

            string fmt = "F" + 3; // e.g., "F3"
            // position
            JSONObject pos = new JSONObject();
            pos["x"] = position.x.ToString(fmt);
            pos["y"] = position.y.ToString(fmt);
            pos["z"] = position.z.ToString(fmt);
            json["position"] = pos;

            // size
            JSONObject s = new JSONObject();
            s["x"] = size.x.ToString(fmt);
            s["y"] = size.y.ToString(fmt);
            s["z"] = size.z.ToString(fmt);
            json["size"] = s;

            // rotation
            JSONObject rot = new JSONObject();
            rot["x"] = rotation.x.ToString(fmt);
            rot["y"] = rotation.y.ToString(fmt);
            rot["z"] = rotation.z.ToString(fmt);
            json["rotation"] = rot;
            return json;
        }
    }

    [SerializeField] private BoxCollider _boxCollider;
    [TextArea(5, 20)] // Inspector 中多行显示
    public string jsonOutput;
    
    private void Awake()
    {
        // 尝试获取 BoxCollider 组件
        _boxCollider = GetComponent<BoxCollider>();
    }
    
    public JSONObject ExportBoxColliderAsJson()
    {
        if (_boxCollider == null)
        {
            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider == null)
            {
                return new JSONObject();
            }
        }
        Transform t = transform;

        // 1. 获取 BoxCollider 的局部属性
        Vector3 localCenter = _boxCollider.center;
        Vector3 localSize = _boxCollider.size;

        // 2. 将局部 center 转换为世界空间位置
        Vector3 worldCenter = t.TransformPoint(localCenter);

        // 3. 计算世界空间的 size（考虑缩放）
        Vector3 worldSize = Vector3.Scale(localSize, t.lossyScale);

        // 4. 获取旋转（欧拉角）
        Vector3 worldRotation = t.rotation.eulerAngles;

        // 5. 构造数据对象
        BoxColliderData data = new BoxColliderData
        {
            position = worldCenter,
            size = worldSize,
            rotation = worldRotation
        };

        return data.toJSONObject();
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
        if (GUILayout.Button("导出 BoxCollider 为 JSON"))
        {
            var json = describer.ExportBoxColliderAsJson();
            describer.jsonOutput = json.ToString(2);
            EditorUtility.SetDirty(describer);
        }
    }
}
#endif
