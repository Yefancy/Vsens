using Animations;
using UnityEditor;
using UnityEngine;

namespace smplx
{
    [CustomEditor(typeof(SmplxBodyAnimationController))]
    public class SmplxBodyAnimationControllerEditor: BodyAnimationControllerEditor
    {
        public override void OnInspectorGUI()
        {
           base.OnInspectorGUI();
           var controller = (SmplxBodyAnimationController) target;
           if (controller.animationFile!= null && GUILayout.Button("Load Animation"))
           {
               controller.setAnimation(controller.animationFile.name, controller.animationFile.text);
           }
        }
    }
}