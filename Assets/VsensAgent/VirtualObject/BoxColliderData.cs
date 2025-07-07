using System;
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

        // position
        JSONArray pos = new JSONArray();
        pos.Add(new JSONNumber(Math.Round(position.x, 3)));
        pos.Add(new JSONNumber(Math.Round(position.y, 3)));
        pos.Add(new JSONNumber(Math.Round(position.z, 3)));
        json["position"] = pos;

        // size
        JSONArray s = new JSONArray();
        s.Add(new JSONNumber(Math.Round(size.x, 3)));
        s.Add(new JSONNumber(Math.Round(size.y, 3)));
        s.Add(new JSONNumber(Math.Round(size.z, 3)));
        json["size"] = s;

        // rotation
        JSONArray rot = new JSONArray();
        rot.Add(new JSONNumber(Math.Round(rotation.x, 3)));
        rot.Add(new JSONNumber(Math.Round(rotation.y, 3)));
        rot.Add(new JSONNumber(Math.Round(rotation.z, 3)));
        json["rotation"] = rot;
        
        return json;
    }
}