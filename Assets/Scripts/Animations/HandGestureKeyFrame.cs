using System.Collections.Generic;
using Oculus.Interaction;
using OVRSimpleJSON;
using UnityEngine;

namespace Animations
{
    public struct HandGestureKeyFrame
    {
        public float time;
        public Pose rootPose;
        public Vector3 worldOffset;
        public List<Pose> handJointPoses;
        
        public static HandGestureKeyFrame Lerp(HandGestureKeyFrame a, HandGestureKeyFrame b, float t)
        {
            var result = new HandGestureKeyFrame();
            result.time = Mathf.Lerp(a.time, b.time, t);
            var output = new Pose();
            PoseUtils.Lerp(a.rootPose, b.rootPose, t, ref output);
            result.rootPose = output;
            result.handJointPoses = new List<Pose>();
            result.worldOffset = Vector3.Lerp(a.worldOffset, b.worldOffset, t);
            for (var i = 0; i < a.handJointPoses.Count; i++)
            {
                var pose = new Pose();
                PoseUtils.Lerp(a.handJointPoses[i], b.handJointPoses[i], t, ref pose);
                result.handJointPoses.Add(pose);
            }
            return result;
        }
        
        public JSONObject serialize()
        {
            var json = new JSONObject();
            json["time"] = time;
            json["rootPose"] = Utils.SerializePose(rootPose);
            json["worldOffset"] = Utils.SerializeVector3(worldOffset);
            var handJointPosesArray = new JSONArray();
            foreach (var pose in handJointPoses)
            {
                handJointPosesArray.Add(Utils.SerializePose(pose));
            }
            json["handJointPoses"] = handJointPosesArray;
            return json;
        }
    }
}