using UnityEditor;
using UnityEngine;

public class Screenshot : MonoBehaviour
{
    [SerializeField] private RenderTexture screenshotTexture;
    
    public void TakeScreenshot()
    {
        if (screenshotTexture == null) return;
        var date = System.DateTime.Now;
        var fileName = date.ToString("yyyy-MM-dd") + ".png";
        var path = Application.persistentDataPath + "/Screenshots/" + fileName;
#if UNITY_EDITOR
        var file = EditorUtility.SaveFilePanel("save screen shot", "", fileName, "png");
        if (string.IsNullOrEmpty(file)) return;
        path = file;
#endif
        Utils.SaveTextureToFile(screenshotTexture, path, screenshotTexture.width, screenshotTexture.height);
    }
}
