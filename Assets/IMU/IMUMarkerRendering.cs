using Sensor;
using UnityEngine;

namespace IMU
{
    public class IMUMarkerRendering : MonoBehaviour
    {
        public Camera cam;
        public VirtualIMUSensor imuSensor;
        public float baseScreenSize = 4.5f;
        public Color color = Color.yellow;

        private Material _material;

        void OnEnable()
        {
            if (cam == null)
            {
                cam = Camera.main;
            }
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _material = new Material(shader);
            _material.hideFlags = HideFlags.HideAndDontSave;
            _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _material.SetInt("_ZWrite", 0);
        }

        void OnRenderObject()
        {
            if (!cam || !_material) return;

            Camera currentCam = Camera.current;
            if (currentCam != cam && currentCam != SceneViewCamera() || imuSensor == null) return;

            Vector3 screenPos = currentCam.WorldToScreenPoint(imuSensor.transform.position);
            if (screenPos.z < 0) return;

            float scale = 1f / screenPos.z;
            float size = baseScreenSize * scale;
            float half = size / 2f;

            float x = screenPos.x;
            float y = screenPos.y;

            _material.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(); // 画屏幕坐标

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);

            // Two triangles to form a solid square
            GL.Vertex3(x - half, y - half, 0);
            GL.Vertex3(x + half, y - half, 0);
            GL.Vertex3(x + half, y + half, 0);

            GL.Vertex3(x + half, y + half, 0);
            GL.Vertex3(x - half, y + half, 0);
            GL.Vertex3(x - half, y - half, 0);

            GL.End();
            GL.PopMatrix();
        }

        private Camera SceneViewCamera()
        {
#if UNITY_EDITOR
            return UnityEditor.SceneView.lastActiveSceneView?.camera;
#else
        return null;
#endif
        }
    }
}
