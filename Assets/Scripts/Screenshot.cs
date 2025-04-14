using UnityEditor;
using UnityEngine;

public class Screenshot : MonoBehaviour
{
    [SerializeField] private RenderTexture screenshotTexture;
    
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(Screenshot))]
    public class EditorScreenshot : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Take Screenshot"))
            {
                Screenshot screenshot = (Screenshot)target;
                if (screenshot.screenshotTexture ==null) return;
                var date = System.DateTime.Now;
                var fileName = date.ToString("yyyy-MM-dd") + ".png";
                var path = Application.persistentDataPath + "/Screenshots/" + fileName;
                var file = EditorUtility.SaveFilePanel("save screen shot", "", fileName, "png");
                if (string.IsNullOrEmpty(file)) return;
                path = file;
                Utils.SaveTextureToFile(screenshot.screenshotTexture, path, screenshot.screenshotTexture.width, screenshot.screenshotTexture.height);
            }
        }
    }
#endif
}
