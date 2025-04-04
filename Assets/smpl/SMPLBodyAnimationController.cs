using Animations;
using UnityEngine;

namespace smpl
{
    public class SmplBodyAnimationController: BodyAnimationController
    {
        public void setAnimation(string name, string animation)
        {
            setAnimation(AnimationUtils.ParseSmplAnimation(name, animation));
        }
        
        public override string[] getJointNames()
        {
            return AnimationUtils.SMPL_JOINTS;
        }

        public override void PrepareModel()
        {
            _bones = new Transform[getJointNames().Length];
            _root = Utils.FindFirstDeepChild(transform, "f_avg_root");
            if (_root == null)
            {
                _root = Utils.FindFirstDeepChild(transform, "m_avg_root");
            }

            if (_root != null)
            {
                // smpl bones
                var prefix = _root.name.Substring(0, 6);
                for (var i = 0; i < getJointNames().Length; i++)
                {
                    var bone = AnimationUtils.DeepFind(_root, prefix + getJointNames()[i]);
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