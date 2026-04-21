using System;
using VsensAgent.Network.Protocol;

namespace VsensAgent.SceneHistory
{
    [Serializable]
    public sealed class SceneActionRecord
    {
        public string actionId;
        public string source;
        public string actionType;
        public string targetId;
        public string timestampUtc;
        public string description;
        public string rawActionJson;
        public string revertedActionId;
        public SceneTransformSnapshot beforeTransform;
        public SceneTransformSnapshot afterTransform;

        public UnitySceneActionRecordLog ToNetworkLog()
        {
            return new UnitySceneActionRecordLog
            {
                actionId = actionId,
                source = source,
                actionType = actionType,
                targetId = targetId,
                timestampUtc = timestampUtc,
                description = description,
                rawActionJson = rawActionJson,
                revertedActionId = revertedActionId,
                beforeTransform = ToNetworkLog(beforeTransform),
                afterTransform = ToNetworkLog(afterTransform)
            };
        }

        private static UnitySceneTransformSnapshotLog ToNetworkLog(SceneTransformSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new UnitySceneTransformSnapshotLog
            {
                objectId = snapshot.objectId,
                position = new UnityVector3Log
                {
                    x = snapshot.position.x,
                    y = snapshot.position.y,
                    z = snapshot.position.z
                },
                rotation = new UnityQuaternionLog
                {
                    x = snapshot.rotation.x,
                    y = snapshot.rotation.y,
                    z = snapshot.rotation.z,
                    w = snapshot.rotation.w
                },
                localScale = new UnityVector3Log
                {
                    x = snapshot.localScale.x,
                    y = snapshot.localScale.y,
                    z = snapshot.localScale.z
                },
                parentName = snapshot.parentName
            };
        }
    }
}
