using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PrefabPreview))]
public class PrefabPreviewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var preview = (PrefabPreview) target;
        
        if (GUILayout.Button("Rendering Preview"))
        {
            preview.StartCoroutine(preview.RenderingPreview(preview.prefab));
        }
        
        if (preview.TextureCache != null && GUILayout.Button("Screen Shot"))
        {
            var texture = preview.TextureCache;
            if (!preview.TextureCache.isReadable)
            {
                Debug.LogWarning("Texture is not readable, creating a new texture");
                RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
                Graphics.Blit(texture, rt);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                texture = new Texture2D(texture.width, texture.height);
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
            byte[] pngData = texture.EncodeToPNG();
            if (pngData != null)
            {
                var path = EditorUtility.SaveFilePanel("Save texture as PNG", "", preview.prefab.name, "png");
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllBytes(path, pngData);
                }
            }
        }
    }
}