using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Animations
{
    public struct AnimationFrame
    {
        public Vector3 translation;
        public Quaternion[] boneRotations;
    }
    
    public class RawAnimation
    {
        public static RawAnimation Empty = new RawAnimation()
        {
            name = "empty",
            fps = -1,
            frames = Array.Empty<AnimationFrame>()
        };
        public string name;
        public float fps;
        public AnimationFrame[] frames;
    }
    
    public class SMPLXAnimation: RawAnimation
    {
        public string gender;
        public float[] betas;
    }
    
    [System.Serializable]
    class SMPLXAnimationRawJson
    {
        public string gender;
        public float fps;
        public float[] betas;
        public float[][] poses;  // shape: [frame][156]
        public float[][] trans;  // shape: [frame][3]
    }
    
    public static class AnimationUtils
    {
        
        public static readonly string[] SMPL_JOINTS =
        {
            "Pelvis", // 0
            "L_Hip", // 1
            "R_Hip", // 2
            "Spine1", // 3
            "L_Knee", // 4
            "R_Knee", // 5
            "Spine2", // 6
            "L_Ankle", // 7
            "R_Ankle", // 8
            "Spine3", // 9
            "L_Foot", // 10
            "R_Foot", // 11
            "Neck", // 12
            "L_Collar", // 13
            "R_Collar", // 14
            "Head", // 15
            "L_Shoulder", // 16
            "R_Shoulder", // 17
            "L_Elbow", // 18
            "R_Elbow", // 19
            "L_Wrist", // 20
            "R_Wrist", // 21
            "L_Hand", // 22
            "R_Hand" // 23
        };
        
        public static readonly string[] SMPLX_JOINTS = {
            "pelvis",
            "left_hip",
            "right_hip",
            "spine1",
            "left_knee",
            "right_knee",
            "spine2",
            "left_ankle",
            "right_ankle",
            "spine3",
            "left_foot",
            "right_foot",
            "neck",
            "left_collar",
            "right_collar",
            "head",
            "left_shoulder",
            "right_shoulder",
            "left_elbow",
            "right_elbow",
            "left_wrist",
            "right_wrist",
            "jaw",
            "left_eye_smplhf",
            "right_eye_smplhf",
            "left_index1",
            "left_index2",
            "left_index3",
            "left_middle1",
            "left_middle2",
            "left_middle3",
            "left_pinky1",
            "left_pinky2",
            "left_pinky3",
            "left_ring1",
            "left_ring2",
            "left_ring3",
            "left_thumb1",
            "left_thumb2",
            "left_thumb3",
            "right_index1",
            "right_index2",
            "right_index3",
            "right_middle1",
            "right_middle2",
            "right_middle3",
            "right_pinky1",
            "right_pinky2",
            "right_pinky3",
            "right_ring1",
            "right_ring2",
            "right_ring3",
            "right_thumb1",
            "right_thumb2",
            "right_thumb3",
            // 123 - 69 - 3 + 1 = 55
        };

        
        public static readonly string[] BoneNamesBanana =
        {
            "Hips", // 0
            "Left Thigh", // 1
            "Right Thigh", // 2
            "Spine 1", // 3
            "Left Leg", // 4
            "Right Leg", // 5
            "Spine 2", // 6
            "Left Foot", // 7
            "Right Foot", // 8
            "Spine 3", // 9
            "Left Toes", // 10
            "Right Toes", // 11
            "Neck", // 12
            "Left Shoulder", // 13
            "Right Shoulder", // 14
            "Head", // 15
            "Left Arm", // 16
            "Right Arm", // 17
            "Left Forearm", // 18
            "Right Forearm", // 19
            "Left Hand", // 20
            "Right Hand", // 21
            "Left Hand Index 1", // 22
            "Right Hand Index 1" // 23
        };


        public static RawAnimation ParseSmplAnimation(string name, string json)
        {
            var data = JsonConvert.DeserializeObject<JObject>(json);
            var frames = data["animation"].Value<JArray>();
            var frameCount = frames.Count;
            var rawAnimations = new AnimationFrame[frameCount];
            for (var i = 0; i < frameCount; i++)
            {
                var frame = frames[i].Value<JObject>();
                var trans = frame["trans"];
                var rotations = frame["rotations"].Value<JArray>();
                var bones = new Quaternion[rotations.Count];
                for (var j = 0; j < rotations.Count; j++)
                {
                    var rot = rotations[j].Value<JArray>();
                    var angles = new Quaternion(rot[0].Value<float>(), rot[1].Value<float>(), rot[2].Value<float>(), rot[3].Value<float>()).eulerAngles;
                    bones[j] = Quaternion.Euler(angles.x, -angles.y, -angles.z);
                    if (j == 0) // if is root bone
                    {
                        bones[j] = Quaternion.Euler(0, -90, 90) * bones[j];
                    }

                }
                rawAnimations[i] = new AnimationFrame()
                {
                    translation = new Vector3(trans[0].Value<float>(), trans[1].Value<float>(), trans[2].ToObject<float>()),
                    boneRotations = bones
                };
            }

            return new RawAnimation()
            {
                name = name,
                fps = -1,
                frames = rawAnimations
            };
        }

        public static SMPLXAnimation ParseSmplxAnimation(string name, string json)
        {
            var raw = JsonConvert.DeserializeObject<SMPLXAnimationRawJson>(json);

            var anim = new SMPLXAnimation
            {
                name = name,
                gender = raw.gender,
                fps = raw.fps,
                betas = raw.betas,
                frames = new AnimationFrame[raw.poses.Length]
            };
            
            for (int i = 0; i < raw.poses.Length; i++)
            {
                var poseVec = raw.poses[i]; // float[156]
                var translation = raw.trans[i]; // float[3]

                var frame = new AnimationFrame();
                frame.translation = new Vector3(translation[0], -translation[1], translation[2]);
                frame.boneRotations = new Quaternion[55];  // 165 / 3 = 55

                for (int j = 0; j < 52; j++)
                {
                    int idx = j * 3;
                    Vector3 axisAngle = new Vector3(poseVec[idx], poseVec[idx + 1], poseVec[idx + 2]);
                    axisAngle = new Vector3(axisAngle.x, -axisAngle.y, -axisAngle.z);
                    frame.boneRotations[j] = AxisAngleToQuaternion(axisAngle);
                }

                anim.frames[i] = frame;
            }

            return anim;
        }
        
        private static Quaternion AxisAngleToQuaternion(Vector3 axisAngle)
        {
            float angle = axisAngle.magnitude;
            if (angle < 1e-8f) return Quaternion.identity;

            Vector3 axis = axisAngle / angle;
            return Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);
        }

        public static Transform DeepFind(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                var found = DeepFind(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}