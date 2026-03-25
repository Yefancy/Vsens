using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VsensAgent.SceneApi.V2
{
    [Serializable]
    public sealed class AvatarValidationCameraPose
    {
        public string Label { get; set; } = string.Empty;
        public Vector3 Position { get; set; }
        public Vector3 RotationEuler { get; set; }
    }

    [Serializable]
    public sealed class AvatarValidationCandidateDebugInfo
    {
        public string CandidateId { get; set; } = string.Empty;
        public Vector3 Position { get; set; }
        public Vector3 RotationEuler { get; set; }
    }

    [Serializable]
    public sealed class AvatarValidationDebugSnapshot
    {
        public string ValidatorContext { get; set; } = string.Empty;
        public Vector3 AvatarRootPosition { get; set; }
        public Vector3 AvatarForward { get; set; } = Vector3.forward;
        public Vector3 AvatarVisualForward { get; set; } = Vector3.forward;
        public Vector3 TargetBoundsCenter { get; set; }
        public Vector3 TargetBoundsSize { get; set; }
        public bool Passed { get; set; }
        public float Score { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public string CurrentCandidateId { get; set; } = string.Empty;
        public IReadOnlyList<AvatarValidationCameraPose> ValidationCameraPoses { get; set; } = Array.Empty<AvatarValidationCameraPose>();
        public IReadOnlyList<AvatarValidationCandidateDebugInfo> Candidates { get; set; } = Array.Empty<AvatarValidationCandidateDebugInfo>();
    }

    public static class AvatarValidationDebugState
    {
        private sealed class CandidateContext
        {
            public string CurrentCandidateId { get; set; } = string.Empty;
            public IReadOnlyList<AvatarValidationCandidateDebugInfo> Candidates { get; set; } = Array.Empty<AvatarValidationCandidateDebugInfo>();
        }

        private static readonly Dictionary<string, CandidateContext> ContextByValidator = new();

        public static AvatarValidationDebugSnapshot Latest { get; private set; }

        public static void RegisterCandidateContext(
            string validatorContext,
            IEnumerable<AvatarValidationCandidateDebugInfo> candidates,
            string currentCandidateId)
        {
            var key = NormalizeContext(validatorContext);
            ContextByValidator[key] = new CandidateContext
            {
                CurrentCandidateId = currentCandidateId ?? string.Empty,
                Candidates = candidates?.ToList() ?? new List<AvatarValidationCandidateDebugInfo>()
            };
        }

        public static void UpdateCurrentCandidate(string validatorContext, string currentCandidateId)
        {
            var key = NormalizeContext(validatorContext);
            if (!ContextByValidator.TryGetValue(key, out var context))
            {
                return;
            }

            context.CurrentCandidateId = currentCandidateId ?? string.Empty;
        }

        public static void Publish(AvatarValidationDebugSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var key = NormalizeContext(snapshot.ValidatorContext);
            if (ContextByValidator.TryGetValue(key, out var context))
            {
                snapshot.CurrentCandidateId = context.CurrentCandidateId;
                snapshot.Candidates = context.Candidates;
            }

            Latest = snapshot;
        }

        public static void ClearContext(string validatorContext)
        {
            var key = NormalizeContext(validatorContext);
            ContextByValidator.Remove(key);
        }

        private static string NormalizeContext(string validatorContext)
        {
            return string.IsNullOrWhiteSpace(validatorContext) ? "avatar_validation" : validatorContext.Trim();
        }
    }
}
