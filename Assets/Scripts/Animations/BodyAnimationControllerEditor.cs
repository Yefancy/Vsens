using UnityEditor;
using UnityEngine;

namespace Animations
{
    [CustomEditor(typeof(BodyAnimationController))]
    public class BodyAnimationControllerEditor: Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (BodyAnimationController) target;
            DrawDefaultInspector();
            if (!controller.hasAnimation) return;
            
            if (controller.isPlaying)
            {
                if (GUILayout.Button("Pause"))
                {
                    controller.isPlaying = false;
                }
            }
            else
            {
                if (GUILayout.Button("Play"))
                {
                    controller.isPlaying = true;
                }
            }
            
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUILayout.Slider("progress", controller.normalizedTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                controller.normalizedTime = newValue;
                controller.PlayAnimationToTime();
            }

            // 可选：显示当前进度的百分比信息
            EditorGUILayout.LabelField("progress：" + (controller.normalizedTime * 100f).ToString("F1") + "%");
        }
    }
}