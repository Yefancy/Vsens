using Oculus.Interaction;
using UnityEditor;
using UnityEngine;

namespace Vsens.ui
{
    [RequireComponent(typeof(PointableCanvas))]
    [RequireComponent(typeof(BoxCollider))]
    public class UIPanel : MonoBehaviour
    {
        public PointableCanvas Canvas;
        public BoxCollider CanvasCollider;
        
        private void Awake()
        {
            if (Canvas == null)
            {
                Canvas = GetComponent<PointableCanvas>();
            }
            if (CanvasCollider == null)
            {
                CanvasCollider = GetComponent<BoxCollider>();
            }
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(UIPanel))]
    public class UIPanelEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var panel = (UIPanel)target;
            if (panel.Canvas ==null)
            {
                panel.Canvas = panel.GetComponent<PointableCanvas>();
            }
            if (panel.CanvasCollider == null)
            {
                panel.CanvasCollider = panel.GetComponent<BoxCollider>();
            }
            if (panel.Canvas == null)
            {
                EditorGUILayout.HelpBox("Canvas is not assigned!", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox("Canvas is assigned!", MessageType.Info);
            }
            if (GUILayout.Button("Auto Update Collider"))
            {
                var canvasSize = ((RectTransform)panel.Canvas.Canvas.transform).rect.size;
                var canvasScale = panel.Canvas.Canvas.transform.localScale;
                var colliderSize = new Vector3(canvasSize.x * canvasScale.x, canvasSize.y * canvasScale.y, 0.01f);
                panel.CanvasCollider.size = colliderSize;
            }
        }
    }
#endif
}

