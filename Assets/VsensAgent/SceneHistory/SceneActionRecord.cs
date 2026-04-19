using System;

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

        public SceneActionRecord CloneForLogOnly()
        {
            return new SceneActionRecord
            {
                actionId = actionId,
                source = source,
                actionType = actionType,
                targetId = targetId,
                timestampUtc = timestampUtc,
                description = description,
                rawActionJson = rawActionJson,
                revertedActionId = revertedActionId,
                beforeTransform = beforeTransform,
                afterTransform = afterTransform
            };
        }
    }
}
