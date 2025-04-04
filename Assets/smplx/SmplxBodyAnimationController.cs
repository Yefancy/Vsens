using Animations;
using UnityEngine;

namespace smplx
{
    [RequireComponent(typeof(SMPLX))]
    public class SmplxBodyAnimationController: BodyAnimationController
    {
        private SMPLX smplx;
        
        public void setAnimation(string name, string animation)
        {
            setAnimation(AnimationUtils.ParseSmplxAnimation(name, animation));
        }
        
        public override void setAnimation(RawAnimation animation)
        {
            if (_root != null)
            {
                _root.parent.localEulerAngles = new Vector3(-90, 0, 0);
                _root.parent.localPosition = new Vector3(0, -0.4f, -0.4f);
            }
            
            base.setAnimation(animation);
            if (animation is SMPLXAnimation smplxAnimation)
            {
                SetBetas(smplxAnimation.betas);
            }
        }
        
        public void SetBetas(float[] betas)
        {
            if (smplx != null)
            {
                // store current transform
                var globalTranslation = _root.localPosition;

                _root.localPosition = Vector3.zero;
                _root.parent.localEulerAngles = Vector3.zero;
                _root.parent.localPosition = Vector3.zero;
                
                smplx.betas = betas;
                smplx.SetBetaShapes();
                
                // restore transform
                _root.localPosition = globalTranslation;
                _root.parent.localEulerAngles = new Vector3(-90, 0, 0);
                _root.parent.localPosition = new Vector3(0, -0.4f, -0.4f);
            }
        }
        
        public override string[] getJointNames()
        {
            return AnimationUtils.SMPLX_JOINTS;
        }

        public override void PrepareModel()
        {
            smplx = GetComponent<SMPLX>();
            _bones = new Transform[getJointNames().Length];
            _root = Utils.FindFirstDeepChild(transform, "root");
            
            if (_root != null)
            {
                // smpl bones
                for (var i = 0; i < getJointNames().Length; i++)
                {
                    var bone = AnimationUtils.DeepFind(_root, getJointNames()[i]);
                    if (bone == null)
                    {
                        Debug.LogError("Bone not found: " + getJointNames()[i]);
                    }
                    _bones[i] = bone;
                }
                _initialBones = GetCurrentPose();
            }
            
            if (animationFile != null)
            {
                setAnimation(animationFile.name, animationFile.text);
            }
        }
    }
}