using Animations;
using UnityEditor;
using UnityEngine;

namespace smpl
{
    [CustomEditor(typeof(SmplBodyAnimationController))]
    public class SmplBodyAnimationControllerEditor: BodyAnimationControllerEditor
    {
        public override void OnInspectorGUI()
        {
           base.OnInspectorGUI();
           var controller = (SmplBodyAnimationController) target;
           if (controller.animationFile!= null && GUILayout.Button("Load Animation"))
           {
               controller.setAnimation(controller.animationFile.name, controller.animationFile.text);
           }
        }
    }
}