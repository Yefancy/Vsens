using System;
using JetBrains.Annotations;
using OVRSimpleJSON;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public static class Utils
{
    public static float NormalizeAngle(float angle)
    {
        angle = angle % 360;
        if (angle > 180)
            angle -= 360;
        else if (angle < -180)
            angle += 360;
        return angle;
    }

    public static Vector3 NormalizeAngles(Vector3 angles)
    {
        angles.x = NormalizeAngle(angles.x);
        angles.y = NormalizeAngle(angles.y);
        angles.z = NormalizeAngle(angles.z);
        return angles;
    }
    
    [CanBeNull]
    public static Transform FindFirstDeepChild(Transform aParent, string aName) {
        foreach(Transform child in aParent) {
            if(child.name == aName)
                return child;
            Transform result = FindFirstDeepChild(child, aName);
            if (result != null)
                return result;
        }
        return null;
    }
    
    public static Vector3 CalculateNormal(Vector3 A, Vector3 B, Vector3 C)
    {
        var side1 = B - A;
        var side2 = C - A;
        var normal = Vector3.Cross(side1, side2).normalized; // 叉积并标准化
        return normal.normalized;
    }
    
    public enum SaveTextureFileFormat
    {
        EXR, JPG, PNG, TGA
    }
    
    public static void SaveTextureToFile(Texture source,
                                         string filePath,
                                         int width,
                                         int height,
                                         SaveTextureFileFormat fileFormat = SaveTextureFileFormat.PNG,
                                         int jpgQuality = 95,
                                         bool asynchronous = true,
                                         Action<bool> done = null)
    {
        // check that the input we're getting is something we can handle:
        if (!(source is Texture2D || source is RenderTexture))
        {
            done?.Invoke(false);
            return;
        }
 
        // use the original texture size in case the input is negative:
        if (width < 0 || height < 0)
        {
            width = source.width;
            height = source.height;
        }
 
        // resize the original image:
        var resizeRT = RenderTexture.GetTemporary(width, height, 0);
        Graphics.Blit(source, resizeRT);
 
        // create a native array to receive data from the GPU:
        var rawData = new NativeArray<byte>(width * height * 4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
 
        // request the texture data back from the GPU:
        var request = AsyncGPUReadback.RequestIntoNativeArray (ref rawData, resizeRT, 0, (AsyncGPUReadbackRequest request) =>
        {
            // if the readback was successful, encode and write the results to disk
            if (!request.hasError)
            {
                NativeArray<byte> encoded;
 
                switch (fileFormat)
                {
                    case SaveTextureFileFormat.EXR:
                        encoded = ImageConversion.EncodeNativeArrayToEXR(rawData, resizeRT.graphicsFormat, (uint)width, (uint)height);
                        break;
                    case SaveTextureFileFormat.JPG:
                        encoded = ImageConversion.EncodeNativeArrayToJPG(rawData, resizeRT.graphicsFormat, (uint)width, (uint)height, 0, jpgQuality);
                        break;
                    case SaveTextureFileFormat.TGA:
                        encoded = ImageConversion.EncodeNativeArrayToTGA(rawData, resizeRT.graphicsFormat, (uint)width, (uint)height);
                        break;
                    default:
                        encoded = ImageConversion.EncodeNativeArrayToPNG(rawData, resizeRT.graphicsFormat, (uint)width, (uint)height);
                        break;
                }
                var parentDir = System.IO.Path.GetDirectoryName(filePath);
                if (parentDir == null) return;
                if (!System.IO.Directory.Exists(parentDir))
                {
                    System.IO.Directory.CreateDirectory(parentDir);
                }
                System.IO.File.WriteAllBytes(filePath, encoded.ToArray());
                encoded.Dispose();
            }
 
            rawData.Dispose();
 
            // notify the user that the operation is done, and its outcome.
            done?.Invoke(!request.hasError);
        });
 
        if (!asynchronous)
            request.WaitForCompletion();
    }
    
    public static string FormatTime(int totalSeconds)
    {
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        var formattedTime = "";

        if (hours > 0)
        {
            formattedTime += $"{hours}h";
        }

        if (minutes > 0)
        {
            formattedTime += $"{minutes}m";
        }

        formattedTime += $"{seconds}s";

        return formattedTime;
    }
    
    public static JSONObject SerializeVector3(Vector3 vector3)
    {
        var json = new JSONObject
        {
            ["x"] = vector3.x,
            ["y"] = vector3.y,
            ["z"] = vector3.z
        };
        return json;
    }
    
    public static JSONObject SerializePose(Pose pose)
    {
        var json = new JSONObject
        {
            ["position"] = SerializeVector3(pose.position),
            ["rotation"] = SerializeVector3(pose.rotation.eulerAngles)
        };
        return json;
    }
}