#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VsensAgent.SceneApi.V2;

namespace VsensAgent.Editor
{
    [InitializeOnLoad]
    public static class AvatarValidationSceneDebugDrawer
    {
        private const string OverlayPrefKey = "VsensAgent.AvatarValidationSceneDebugDrawer.Enabled";

        static AvatarValidationSceneDebugDrawer()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        [MenuItem("Tools/Vsens/Avatar Validation Debug Overlay")]
        private static void ToggleOverlay()
        {
            Enabled = !Enabled;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Vsens/Avatar Validation Debug Overlay", true)]
        private static bool ToggleOverlayValidate()
        {
            Menu.SetChecked("Tools/Vsens/Avatar Validation Debug Overlay", Enabled);
            return true;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(OverlayPrefKey, true);
            set => EditorPrefs.SetBool(OverlayPrefKey, value);
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!Enabled)
            {
                return;
            }

            var snapshot = AvatarValidationDebugState.Latest;
            if (TryGetLiveAvatarPose(out var liveAvatarPosition, out var liveAvatarForward, out var liveAvatarVisualForward))
            {
                DrawAvatar(liveAvatarPosition, liveAvatarForward, liveAvatarVisualForward, "avatar_root");
            }

            if (snapshot == null)
            {
                DrawHeader(null);
                return;
            }

            DrawTarget(snapshot);
            DrawAvatar(snapshot.AvatarRootPosition, snapshot.AvatarForward, snapshot.AvatarVisualForward, "avatar_root (scored)");
            DrawCandidates(snapshot);
            DrawValidationCameras(snapshot);
            DrawHeader(snapshot);
        }

        private static void DrawTarget(AvatarValidationDebugSnapshot snapshot)
        {
            using (new Handles.DrawingScope(new Color(1f, 0.55f, 0.1f, 0.95f)))
            {
                Handles.DrawWireCube(snapshot.TargetBoundsCenter, snapshot.TargetBoundsSize);
                Handles.SphereHandleCap(
                    0,
                    snapshot.TargetBoundsCenter,
                    Quaternion.identity,
                    0.08f,
                    EventType.Repaint);
                Handles.Label(snapshot.TargetBoundsCenter + Vector3.up * 0.18f, "target");
            }
        }

        private static void DrawAvatar(Vector3 root, Vector3 forward, Vector3 visualForward, string label)
        {
            var upOffset = Vector3.up * 0.02f;
            var quadSize = 0.18f;
            var quad = new[]
            {
                root + upOffset + new Vector3(-quadSize, 0f, -quadSize),
                root + upOffset + new Vector3(-quadSize, 0f, quadSize),
                root + upOffset + new Vector3(quadSize, 0f, quadSize),
                root + upOffset + new Vector3(quadSize, 0f, -quadSize)
            };

            using (new Handles.DrawingScope(new Color(0.2f, 0.95f, 0.95f, 0.2f)))
            {
                Handles.DrawSolidRectangleWithOutline(quad, new Color(0.2f, 0.95f, 0.95f, 0.12f), new Color(0.2f, 0.95f, 0.95f, 0.95f));
            }

            using (new Handles.DrawingScope(new Color(0.15f, 0.8f, 1f, 1f)))
            {
                Handles.ArrowHandleCap(
                    0,
                    root + Vector3.up * 0.05f,
                    Quaternion.LookRotation(forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward),
                    0.65f,
                    EventType.Repaint);
                Handles.Label(root + Vector3.up * 0.22f, label);
            }

            using (new Handles.DrawingScope(new Color(0.25f, 1f, 0.35f, 1f)))
            {
                Handles.ArrowHandleCap(
                    0,
                    root + Vector3.up * 0.08f,
                    Quaternion.LookRotation(visualForward.sqrMagnitude > 0.0001f ? visualForward.normalized : Vector3.forward),
                    0.55f,
                    EventType.Repaint);
                Handles.Label(root + Vector3.up * 0.34f, "visual_forward");
            }
        }

        private static void DrawCandidates(AvatarValidationDebugSnapshot snapshot)
        {
            if (snapshot.Candidates == null)
            {
                return;
            }

            foreach (var candidate in snapshot.Candidates)
            {
                bool isCurrent = candidate.CandidateId == snapshot.CurrentCandidateId;
                var color = isCurrent ? new Color(1f, 0.92f, 0.2f, 1f) : new Color(0.65f, 0.65f, 0.65f, 0.9f);
                var pos = candidate.Position + Vector3.up * 0.015f;

                using (new Handles.DrawingScope(color))
                {
                    Handles.DrawSolidDisc(pos, Vector3.up, isCurrent ? 0.14f : 0.1f);
                    var forward = Quaternion.Euler(candidate.RotationEuler) * Vector3.forward;
                    Handles.DrawLine(pos, pos + forward.normalized * 0.45f);
                    Handles.Label(pos + Vector3.up * 0.08f, isCurrent ? $"{candidate.CandidateId} (current)" : candidate.CandidateId);
                }
            }
        }

        private static void DrawValidationCameras(AvatarValidationDebugSnapshot snapshot)
        {
            if (snapshot.ValidationCameraPoses == null)
            {
                return;
            }

            using (new Handles.DrawingScope(new Color(0.85f, 0.35f, 1f, 0.95f)))
            {
                foreach (var pose in snapshot.ValidationCameraPoses)
                {
                    var pos = pose.Position;
                    Handles.ConeHandleCap(0, pos, Quaternion.Euler(pose.RotationEuler), 0.18f, EventType.Repaint);
                    Handles.DrawDottedLine(pos, snapshot.TargetBoundsCenter, 4f);
                    Handles.Label(pos + Vector3.up * 0.12f, pose.Label);
                }
            }
        }

        private static void DrawHeader(AvatarValidationDebugSnapshot snapshot)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 12f, 360f, 120f), "Avatar Validation Debug", GUI.skin.window);
            if (snapshot == null)
            {
                GUILayout.Label("Live avatar overlay active.");
                GUILayout.Label("No validation snapshot yet.");
                GUILayout.Label("Run an avatar validation/score step to show target, candidates, and camera poses.");
            }
            else
            {
                GUILayout.Label($"Context: {snapshot.ValidatorContext}");
                GUILayout.Label($"Score: {snapshot.Score:0.000}   Passed: {snapshot.Passed}");
                GUILayout.Label($"Action: {snapshot.RecommendedAction}");
                GUILayout.Label($"Current candidate: {snapshot.CurrentCandidateId}");
            }
            GUILayout.Label("Colors: cyan=logic forward, green=visual forward, orange=target, yellow=current candidate, magenta=cameras");
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static bool TryGetLiveAvatarPose(out Vector3 position, out Vector3 forward, out Vector3 visualForward)
        {
            position = default;
            forward = Vector3.forward;
            visualForward = Vector3.forward;

            var runtime = Object.FindFirstObjectByType<AvatarRuntimeManager>();
            var avatar = runtime != null ? runtime.GetManagedAvatarObject() : null;
            if (avatar == null)
            {
                avatar = GameObject.Find("avatar_main");
            }

            if (avatar == null)
            {
                return false;
            }

            position = avatar.transform.position;
            forward = AvatarRuntimeManager.GetLogicalForward(avatar.transform.rotation);
            var controller = avatar.GetComponentInChildren<smplx.SmplxBodyAnimationController>(true);
            if (controller != null && controller.Root != null)
            {
                visualForward = controller.Root.forward;
            }
            else
            {
                visualForward = forward;
            }
            return true;
        }
    }
}
#endif
