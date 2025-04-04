using System;
using UnityEngine;

namespace Animations
{
    public interface IAnimationRuntime
    {
        GameObject getGameObject();
        
        BodyAnimationController getAnimationController();
        
        SkinnedMeshRenderer getSkinnedMeshRenderer();

        void UpdateBodyShape();

        void SetBodyShape(Tuple<int, float>[] bodyShapes)
        {
            var skinnedMeshRenderer = getSkinnedMeshRenderer();
            if (skinnedMeshRenderer == null) return;
            foreach (var pair in bodyShapes)
            {
                skinnedMeshRenderer.SetBlendShapeWeight(pair.Item1, pair.Item2);
            }
            UpdateBodyShape();
        }
    }
}