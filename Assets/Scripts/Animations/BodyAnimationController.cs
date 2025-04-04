using UnityEngine;

namespace Animations
{
    public abstract class BodyAnimationController : MonoBehaviour
    {
        
        [SerializeField] public TextAsset animationFile;
        [SerializeField] private float fps = 24;
        [SerializeField] public float speed = 1;
        [SerializeField] public bool globalTranslation = false;
        [SerializeField] public float amplitude = 1f;
        [SerializeField] private SkinCollider skinCollider;
        
        // runtime
        protected Transform _root;
        protected Transform[] _bones;
        protected Quaternion[] _initialBones;
        protected Quaternion[] _initialMotionBones;
        protected RawAnimation _rawAnimation;
        
        public float deltaTime
        {
            get
            {
                if (_rawAnimation is { fps: > 0 })
                {
                    return 1f / _rawAnimation.fps;
                }
                return 1f / fps;
            }
        }
    
        public string animationName => _rawAnimation?.name ?? "no_animation";
        public bool isPlaying { get; set; } = true;
        public float AnimationPlayTime => frameCount * deltaTime * speed;
        public int frameCount => _rawAnimation?.frames.Length ?? 0;
        public AnimationFrame getFrame(int index) => _rawAnimation.frames[((index % frameCount) + frameCount) % frameCount];
        public float time { set; get; }
        public float normalizedTime
        {
            get => time / (deltaTime * frameCount);
            set => time = value * (deltaTime * frameCount);
        }
    
        public Quaternion[] GetInitialPose => _initialBones;
        public Quaternion[] GetInitialMotionPose => _initialMotionBones;
        public bool hasAnimation => _rawAnimation != null;
    
        public virtual void setAnimation(RawAnimation animation)
        {
            _rawAnimation = animation;
            SetInitialMotionPose(getPose(0));
            SetCurrentPose(_initialMotionBones);
        }
        
        public void removeAnimation()
        {
            _rawAnimation = null;
        }
    
        public virtual RawAnimation getAnimation()
        {
            return _rawAnimation;
        }
        
        public Transform Root => _root;
        public Transform[] Bones => _bones;

        public abstract string[] getJointNames();
        
        public abstract void PrepareModel();
        
        public void Start()
        {
            PrepareModel();
        }

        public void SetInitialMotionPose(Quaternion[] pose)
        {
            _initialMotionBones = pose;
        }

        public Quaternion[] getPose(float animationTime, bool normalize = false)
        {
            if (normalize)
            {
                animationTime *= deltaTime * frameCount;
            }
            var frameIndex = (int) (animationTime / deltaTime);
            var lerp = (animationTime % deltaTime) / deltaTime;
            var lastFrame = getFrame(frameIndex);
            var nextFrame = getFrame(frameIndex + 1);
            var poses = new Quaternion[getJointNames().Length];
            for (var i = 0; i < lastFrame.boneRotations.Length; i++)
            {
                var bone = _bones[i];
                if (bone != null)
                {
                    var targetRotation = Quaternion.Slerp(lastFrame.boneRotations[i], nextFrame.boneRotations[i], lerp);
                    poses[i] = targetRotation;
                }
            }
            return poses;
        }
        
        public Vector3 getGlobalTranslation(float animationTime)
        {
            if (_rawAnimation == null || frameCount == 0)
            {
                return Vector3.zero;
            }
            var frameIndex = (int) (animationTime / deltaTime);
            var lerp = (animationTime % deltaTime) / deltaTime;
            var lastFrame = getFrame(frameIndex);
            var nextFrame = getFrame(frameIndex + 1);
            var translation = Vector3.Lerp(lastFrame.translation, nextFrame.translation, lerp) - _rawAnimation.frames[0].translation;
            translation = new Vector3(-translation.x, -translation.y, -translation.z);
            return translation;
        }
    
        public Quaternion[] GetCurrentPose()
        {
            var poses = new Quaternion[getJointNames().Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                var bone = _bones[i];
                if (bone != null)
                {
                    poses[i] = bone.localRotation;
                }
            }
            return poses;
        }
    
        public void SetCurrentPose(Quaternion[] poses)
        {
            if (_root != null)
            {
                for (var i = 0; i < _bones.Length; i++)
                {
                    var bone = _bones[i];
                    if (bone != null)
                    {
                        bone.localRotation = poses[i];
                    }
                }
            }
        }
        
        public void SetCurrentGlobalTranslation(Vector3 translation)
        {
            if (_root != null)
            {
                _root.localPosition = translation;
            }
        }

        public void PlayAnimationToTime()
        {
            PlayAnimationTo(time);
        }

        public void PlayAnimationTo(float animationTime)
        {
            if (_root != null)
            {
                var frameIndex = (int) (animationTime / deltaTime);
                var lerp = (animationTime % deltaTime) / deltaTime;
                var lastFrame = getFrame(frameIndex);
                var nextFrame = getFrame(frameIndex + 1);
                if (globalTranslation)
                {
                    var translation = Vector3.Lerp(lastFrame.translation, nextFrame.translation, lerp) - _rawAnimation.frames[0].translation;
                    translation = new Vector3(-translation.x, -translation.y, -translation.z);
                    _root.localPosition = translation;
                }
                else
                {
                    _root.localPosition = Vector3.zero;
                }
                for (var i = 0; i < lastFrame.boneRotations.Length; i++)
                {
                    var bone = _bones[i];
                    if (bone != null)
                    {
                        var targetRotation = Quaternion.Slerp(lastFrame.boneRotations[i], nextFrame.boneRotations[i], lerp);
                        if (amplitude is (< 1 or > 1) and >= 0)
                        {
                            targetRotation = Quaternion.Slerp(_initialMotionBones[i], targetRotation, amplitude);
                        }  
                        bone.localRotation = targetRotation;
                    }
                }
                skinCollider?.ScheduleColliderUpdating();
            }
        }    
    
        private void FixedUpdate()
        {
            if (isPlaying)
            {
                RunNextFrame(Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// used for replay, run to the next frame
        /// </summary>
        /// <param name="fixedDeltaTime"></param>
        public void RunNextFrame(float fixedDeltaTime)
        {
            if (_rawAnimation == null || frameCount == 0)
            {
                return;
            }
            time += fixedDeltaTime * speed;
            PlayAnimationToTime();
        }

    }
}
