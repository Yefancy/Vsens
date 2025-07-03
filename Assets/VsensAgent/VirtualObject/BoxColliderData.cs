using SimpleJSON;
using UnityEngine;

[System.Serializable]
public class BoxColliderData
{
    public Vector3 position;   // 世界坐标
    public Vector3 size;       // 世界空间下 size
    public Vector3 rotation;   // 欧拉角形式的旋转
    
    public static BoxColliderData FromBoxCollider(BoxCollider boxCollider, Transform transform)
    {
        // 获取局部属性
        Vector3 localCenter = boxCollider.center;
        Vector3 localSize = boxCollider.size;

        // 转换为世界空间
        Vector3 worldCenter = transform.TransformPoint(localCenter);
        Vector3 worldSize = Vector3.Scale(localSize, transform.lossyScale);
        Vector3 worldRotation = transform.rotation.eulerAngles;

        return new BoxColliderData
        {
            position = worldCenter,
            size = worldSize,
            rotation = worldRotation
        };
    }
    
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