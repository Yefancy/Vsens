using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Animations;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VsensAgent.SceneApi.V2
{
    public class SceneValidationService
    {
        private const float DefaultAvatarRadius = 0.28f;
        private const float DefaultAvatarHeight = 1.72f;
        private const float DefaultFloorSnapDistance = 2.5f;
        private const float DefaultGroundTolerance = 0.08f;
        private const float TranslationalMotionThreshold = 0.35f;
        private const int DefaultTrajectorySampleCount = 7;
        private const int DefaultMaxCandidates = 4;

        private readonly SceneRegistry _registry;
        private readonly AvatarRuntimeManager _avatarRuntimeManager;

        public SceneValidationService(SceneRegistry registry, AvatarRuntimeManager avatarRuntimeManager = null)
        {
            _registry = registry;
            _avatarRuntimeManager = avatarRuntimeManager;
        }

        public object QueryAvatarCandidates(
            string targetObjectId,
            string targetAlias,
            string taskHint,
            string preferredSide,
            float? preferredDistance,
            int? maxCandidates)
        {
            var snapshot = _registry.BuildSnapshot(includeRelations: false);
            var target = ResolveTarget(snapshot, targetObjectId, targetAlias);
            if (target == null)
            {
                return new
                {
                    type = "scene.query_response",
                    method = "scene.query_avatar_candidates",
                    scene_version = snapshot.scene_version,
                    candidates = new List<AvatarCandidateQueryModel>(),
                    errors = new[]
                    {
                        new ValidationIssueModel
                        {
                            code = SceneApiErrorCodes.TARGET_NOT_FOUND,
                            message = $"Target '{targetObjectId ?? targetAlias}' not found."
                        }
                    }
                };
            }

            var candidates = BuildAvatarCandidates(
                snapshot,
                target,
                taskHint,
                preferredSide,
                preferredDistance ?? GetPreferredDistance(taskHint, target),
                Mathf.Max(1, maxCandidates ?? DefaultMaxCandidates));

            return new
            {
                type = "scene.query_response",
                method = "scene.query_avatar_candidates",
                scene_version = snapshot.scene_version,
                target_object_id = target.id,
                task_hint = taskHint ?? string.Empty,
                preferred_side = preferredSide ?? string.Empty,
                candidates
            };
        }

        public object ValidateAvatarPlacement(
            string targetObjectId,
            string targetAlias,
            string taskHint,
            Vector3? position,
            Vector3? rotationEuler,
            string motionJson,
            int? trajectorySampleCount)
        {
            var snapshot = _registry.BuildSnapshot(includeRelations: false);
            var target = ResolveTarget(snapshot, targetObjectId, targetAlias);
            if (target == null)
            {
                return BuildValidationResponse(
                    snapshot.scene_version,
                    new AvatarPlacementValidationModel
                    {
                        validator_id = Guid.NewGuid().ToString("N"),
                        target_object_id = targetObjectId ?? targetAlias ?? string.Empty,
                        task_hint = taskHint ?? string.Empty,
                        valid = false,
                        collision_free = false,
                        grounded = false,
                        endpoint_valid = false,
                        trajectory_valid = false,
                        motion_mode = "static",
                        issues = new List<ValidationIssueModel>
                        {
                            new ValidationIssueModel
                            {
                                code = SceneApiErrorCodes.TARGET_NOT_FOUND,
                                message = $"Target '{targetObjectId ?? targetAlias}' not found."
                            }
                        }
                    });
            }

            var posePosition = position ?? GetFallbackPosePosition(target, taskHint);
            var poseRotation = rotationEuler ?? ComputeFacingRotation(posePosition, ToVector3(target.bounds_center));
            var report = EvaluatePlacement(
                snapshot,
                target,
                posePosition,
                poseRotation,
                taskHint,
                motionJson,
                Mathf.Max(3, trajectorySampleCount ?? DefaultTrajectorySampleCount));
            return BuildValidationResponse(snapshot.scene_version, report);
        }

        public object CaptureValidationViews(
            string validatorContext,
            string avatarId,
            string targetObjectId,
            string targetAlias)
        {
            var snapshot = _registry.BuildSnapshot(includeRelations: false);
            var target = ResolveTarget(snapshot, targetObjectId, targetAlias);
            var avatar = _avatarRuntimeManager != null ? _avatarRuntimeManager.GetManagedAvatarObject() : null;
            if (avatar == null || target == null)
            {
                return new
                {
                    type = "scene.query_response",
                    method = "scene.capture_validation_views",
                    scene_version = snapshot.scene_version,
                    artifacts = new List<ValidationArtifactModel>(),
                    errors = new[]
                    {
                        new ValidationIssueModel
                        {
                            code = SceneApiErrorCodes.INVALID_PARAM,
                            message = "Avatar and target are required to capture validation views."
                        }
                    }
                };
            }

            var artifacts = CaptureValidationArtifacts(
                validatorContext ?? "validation",
                avatar,
                target);

            AvatarValidationDebugState.Publish(new AvatarValidationDebugSnapshot
            {
                ValidatorContext = validatorContext ?? "validation",
                AvatarRootPosition = avatar.transform.position,
                AvatarForward = AvatarRuntimeManager.GetLogicalForward(avatar.transform.rotation),
                AvatarVisualForward = ResolveAvatarVisualForward(avatar),
                TargetBoundsCenter = ToVector3(target.bounds_center),
                TargetBoundsSize = ToVector3(target.bounds_size),
                Passed = false,
                Score = 0f,
                RecommendedAction = "captured",
                ValidationCameraPoses = artifacts
                    .Select(artifact => new AvatarValidationCameraPose
                    {
                        Label = artifact.label ?? string.Empty,
                        Position = ToVector3(artifact.camera_position),
                        RotationEuler = ToVector3(artifact.camera_rotation)
                    })
                    .ToList()
            });

            return new
            {
                type = "scene.query_response",
                method = "scene.capture_validation_views",
                scene_version = snapshot.scene_version,
                validator_context = validatorContext ?? string.Empty,
                artifacts
            };
        }

        private List<ValidationArtifactModel> CaptureValidationArtifacts(
            string validatorContext,
            GameObject avatar,
            SceneObjectModel target)
        {
            var artifacts = new List<ValidationArtifactModel>();
            var avatarEyePose = ResolveAvatarEyePose(avatar);
            var userCamera = ResolveUserCamera();

            var avatarEyeArtifact = CaptureView(
                validatorContext,
                "avatar_eye",
                avatarEyePose.position,
                avatarEyePose.rotationEuler,
                avatarEyePose.lookAt);
            if (avatarEyeArtifact != null)
            {
                artifacts.Add(avatarEyeArtifact);
            }

            if (userCamera != null)
            {
                var userCameraArtifact = CaptureView(
                    validatorContext,
                    "user_camera",
                    userCamera.transform.position,
                    userCamera.transform.eulerAngles,
                    userCamera.transform.position + userCamera.transform.forward * 2f);
                if (userCameraArtifact != null)
                {
                    artifacts.Add(userCameraArtifact);
                }
            }

            return artifacts;
        }

        private static (Vector3 position, Vector3 rotationEuler, Vector3 lookAt) ResolveAvatarEyePose(GameObject avatar)
        {
            var head = FindAvatarEyeTransform(avatar);
            if (head != null)
            {
                var forward = AvatarRuntimeManager.GetLogicalForward(avatar.transform.rotation);
                return (
                    head.position,
                    Quaternion.LookRotation(forward.sqrMagnitude > 0.0001f ? forward : head.forward, Vector3.up).eulerAngles,
                    head.position + (forward.sqrMagnitude > 0.0001f ? forward.normalized : head.forward.normalized) * 2f
                );
            }

            var logicalForward = AvatarRuntimeManager.GetLogicalForward(avatar.transform.rotation);
            var root = avatar.transform.position;
            var eyePosition = root + Vector3.up * 1.6f;
            return (
                eyePosition,
                Quaternion.LookRotation(logicalForward.sqrMagnitude > 0.0001f ? logicalForward : Vector3.forward, Vector3.up).eulerAngles,
                eyePosition + (logicalForward.sqrMagnitude > 0.0001f ? logicalForward.normalized : Vector3.forward) * 2f
            );
        }

        private static Transform FindAvatarEyeTransform(GameObject avatar)
        {
            if (avatar == null)
            {
                return null;
            }

            var candidates = avatar.GetComponentsInChildren<Transform>(true);
            foreach (var candidate in candidates)
            {
                if (candidate == null)
                {
                    continue;
                }

                var lower = candidate.name.ToLowerInvariant();
                if (lower.Contains("eye") || lower == "head" || lower.EndsWith("head"))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Camera ResolveUserCamera()
        {
            var controller = VsensAgent.Core.ServiceLocator.Get<VsensAgent.UserMainCameraControl>();
            if (controller != null)
            {
                var controlledCamera = controller.GetComponent<Camera>();
                if (controlledCamera != null)
                {
                    return controlledCamera;
                }
            }

            return Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
        }

        private static object BuildValidationResponse(int sceneVersion, AvatarPlacementValidationModel report)
        {
            return new
            {
                type = "scene.query_response",
                method = "scene.validate_avatar_placement",
                scene_version = sceneVersion,
                validation = report
            };
        }

        private List<AvatarCandidateQueryModel> BuildAvatarCandidates(
            SceneSnapshot snapshot,
            SceneObjectModel target,
            string taskHint,
            string preferredSide,
            float preferredDistance,
            int maxCandidates)
        {
            var targetCenter = ToVector3(target.bounds_center);
            var targetSize = ToVector3(target.bounds_size);
            var targetRotation = Quaternion.Euler(ToVector3(target.rotation));

            var basis = BuildCandidateBasis(targetRotation);
            var orderedBasis = basis
                .OrderByDescending(item => GetDirectionalPreferenceBonus(item.side, preferredSide))
                .ToList();
            var standOffDistances = BuildStandOffDistances(preferredDistance, targetSize, taskHint);

            var candidates = new List<AvatarCandidateQueryModel>();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            int idx = 0;
            foreach (var (side, direction) in orderedBasis)
            {
                var dir = new Vector3(direction.x, 0f, direction.z).normalized;
                if (dir.sqrMagnitude < 1e-4f)
                {
                    dir = Vector3.forward;
                }

                foreach (var reach in standOffDistances)
                {
                    var rawPosition = targetCenter + dir * reach;
                    var groundedPosition = ClampToFloorArea(snapshot, SnapToFloor(snapshot, rawPosition));
                    string key = $"{Mathf.RoundToInt(groundedPosition.x * 100f)}:{Mathf.RoundToInt(groundedPosition.z * 100f)}:{Mathf.RoundToInt(reach * 100f)}";
                    if (!seenKeys.Add(key))
                    {
                        continue;
                    }

                    var rotation = ComputeFacingRotation(groundedPosition, targetCenter);
                    var report = EvaluatePlacement(snapshot, target, groundedPosition, rotation, taskHint, motionJson: null, DefaultTrajectorySampleCount);

                    candidates.Add(new AvatarCandidateQueryModel
                    {
                        candidate_id = $"avatar_cand_{idx++:D3}",
                        target_object_id = target.id,
                        task_hint = taskHint ?? string.Empty,
                        preferred_side = side,
                        position = ToData(groundedPosition),
                        rotation = ToData(rotation),
                        score = ScoreValidation(report),
                        facing_score = report.facing_score,
                        distance_to_target = report.distance_to_target,
                        grounded = report.grounded,
                        collision_free = report.collision_free,
                        reasons = report.issues.Select(issue => issue.code).ToList()
                    });
                }
            }

            return candidates
                .OrderByDescending(candidate => RankAvatarCandidate(candidate, preferredSide))
                .ThenBy(candidate => candidate.distance_to_target)
                .Take(maxCandidates)
                .ToList();
        }

        private static List<(string side, Vector3 direction)> BuildCandidateBasis(Quaternion targetRotation)
        {
            var front = targetRotation * Vector3.forward;
            var back = targetRotation * Vector3.back;
            var left = targetRotation * Vector3.left;
            var right = targetRotation * Vector3.right;

            return new List<(string side, Vector3 direction)>
            {
                ("front", front),
                ("front_left", (front + left).normalized),
                ("front_right", (front + right).normalized),
                ("left", left),
                ("right", right),
                ("back_left", (back + left).normalized),
                ("back_right", (back + right).normalized),
                ("back", back),
            };
        }

        private static List<float> BuildStandOffDistances(float preferredDistance, Vector3 targetSize, string taskHint)
        {
            float baseReach = preferredDistance + EstimateObjectRadius(targetSize);
            float expandedReach = baseReach + 0.28f;
            if (!string.IsNullOrWhiteSpace(taskHint) && taskHint.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                expandedReach += 0.18f;
            }

            return new List<float> { baseReach, expandedReach };
        }

        private static float RankAvatarCandidate(AvatarCandidateQueryModel candidate, string preferredSide)
        {
            return candidate.score + GetDirectionalPreferenceBonus(candidate.preferred_side, preferredSide);
        }

        private static float GetDirectionalPreferenceBonus(string side, string preferredSide)
        {
            if (string.IsNullOrWhiteSpace(side) || string.IsNullOrWhiteSpace(preferredSide))
            {
                return 0f;
            }

            if (string.Equals(side, preferredSide, StringComparison.OrdinalIgnoreCase))
            {
                return 0.12f;
            }

            if (side.StartsWith(preferredSide + "_", StringComparison.OrdinalIgnoreCase) ||
                side.EndsWith("_" + preferredSide, StringComparison.OrdinalIgnoreCase))
            {
                return 0.06f;
            }

            return 0f;
        }

        private AvatarPlacementValidationModel EvaluatePlacement(
            SceneSnapshot snapshot,
            SceneObjectModel target,
            Vector3 posePosition,
            Vector3 poseRotation,
            string taskHint,
            string motionJson,
            int trajectorySampleCount)
        {
            var issues = new List<ValidationIssueModel>();
            var targetCenter = ToVector3(target.bounds_center);
            var targetSize = ToVector3(target.bounds_size);
            var targetBounds = new Bounds(targetCenter, targetSize);

            var groundedPose = ClampToFloorArea(snapshot, SnapToFloor(snapshot, posePosition));
            bool grounded = Mathf.Abs(posePosition.y - groundedPose.y) <= DefaultGroundTolerance
                && IsWithinFloorFootprint(snapshot, groundedPose);

            var forward = Quaternion.Euler(poseRotation) * Vector3.forward;
            var toTarget = (targetCenter - posePosition);
            var flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            var facingScore = flatToTarget.sqrMagnitude > 1e-4f
                ? Mathf.Clamp01((Vector3.Dot(forward.normalized, flatToTarget.normalized) + 1f) * 0.5f)
                : 1f;

            float distanceToTarget = flatToTarget.magnitude;
            float minDistance = GetMinDistance(taskHint, targetSize);
            float maxDistance = GetMaxDistance(taskHint, targetSize);
            bool endpointValid = distanceToTarget >= minDistance && distanceToTarget <= maxDistance && facingScore >= 0.55f;

            var (bottom, top) = BuildCapsulePoints(posePosition);
            bool collisionFree = !HasBlockingCapsuleOverlap(bottom, top, DefaultAvatarRadius);
            bool snapshotCollisionFree = !HasBlockingSnapshotOverlap(snapshot, target.id, bottom, top, DefaultAvatarRadius);
            bool targetEmbedded = CapsuleIntersectsBounds(bottom, top, DefaultAvatarRadius, targetBounds);

            if (!grounded)
            {
                issues.Add(new ValidationIssueModel { code = "AVATAR_NOT_GROUNDED", message = "Avatar feet are not grounded on the floor." });
            }
            if (!collisionFree || !snapshotCollisionFree)
            {
                issues.Add(new ValidationIssueModel { code = SceneApiErrorCodes.PLACEMENT_COLLISION, message = "Avatar capsule overlaps existing geometry." });
            }
            if (targetEmbedded)
            {
                issues.Add(new ValidationIssueModel { code = "AVATAR_EMBEDDED_TARGET", message = "Avatar intersects the target object's bounds." });
            }
            if (distanceToTarget < minDistance)
            {
                issues.Add(new ValidationIssueModel { code = "AVATAR_TOO_CLOSE", message = "Avatar stands too close to the target." });
            }
            if (distanceToTarget > maxDistance)
            {
                issues.Add(new ValidationIssueModel { code = "AVATAR_TOO_FAR", message = "Avatar stands too far from the target." });
            }
            if (facingScore < 0.55f)
            {
                issues.Add(new ValidationIssueModel { code = "AVATAR_WRONG_FACING", message = "Avatar is not facing the task target well enough." });
            }

            bool trajectoryValid = true;
            string motionMode = "static";
            int sampledPoints = 0;
            if (!string.IsNullOrWhiteSpace(motionJson))
            {
                var motionAnalysis = AnalyzeMotionTrajectory(motionJson, posePosition, poseRotation, trajectorySampleCount);
                motionMode = motionAnalysis.motionMode;
                trajectoryValid = motionAnalysis.valid;
                sampledPoints = motionAnalysis.sampledPointsChecked;
                issues.AddRange(motionAnalysis.issues);
                endpointValid = endpointValid && motionAnalysis.endpointValid;
            }

            var valid = issues.Count == 0 && collisionFree && snapshotCollisionFree && grounded && endpointValid && trajectoryValid;
            return new AvatarPlacementValidationModel
            {
                validator_id = Guid.NewGuid().ToString("N"),
                target_object_id = target.id,
                task_hint = taskHint ?? string.Empty,
                valid = valid,
                collision_free = collisionFree && snapshotCollisionFree,
                grounded = grounded,
                endpoint_valid = endpointValid,
                trajectory_valid = trajectoryValid,
                motion_mode = motionMode,
                facing_score = (float)Math.Round(facingScore, 4),
                distance_to_target = (float)Math.Round(distanceToTarget, 4),
                sampled_points_checked = sampledPoints,
                suggested_position = ToData(groundedPose),
                suggested_rotation = ToData(ComputeFacingRotation(groundedPose, targetCenter)),
                issues = issues
            };
        }

        private (string motionMode, bool valid, bool endpointValid, int sampledPointsChecked, List<ValidationIssueModel> issues)
            AnalyzeMotionTrajectory(string motionJson, Vector3 posePosition, Vector3 poseRotation, int trajectorySampleCount)
        {
            var issues = new List<ValidationIssueModel>();
            if (string.IsNullOrWhiteSpace(motionJson))
            {
                return ("static", true, true, 0, issues);
            }

            var root = JObject.Parse(motionJson);
            var transArray = root["trans"] as JArray;
            if (transArray == null || transArray.Count < 2)
            {
                return ("static", true, true, 0, issues);
            }

            var parsedAnimation = AnimationUtils.ParseSmplxAnimation("validation_motion", motionJson);
            var localPoints = new List<Vector3>();
            foreach (var frame in parsedAnimation.frames)
            {
                localPoints.Add(frame.translation);
            }

            float pathLength = 0f;
            for (int i = 1; i < localPoints.Count; i++)
            {
                pathLength += Vector3.Distance(localPoints[i - 1], localPoints[i]);
            }

            if (pathLength < TranslationalMotionThreshold)
            {
                return ("static", true, true, 0, issues);
            }

            var sampleIndices = BuildSampleIndices(localPoints.Count, trajectorySampleCount);
            var rotation = Quaternion.Euler(poseRotation);
            foreach (int index in sampleIndices)
            {
                var snapshot = _registry.BuildSnapshot(includeRelations: false);
                var worldPosition = posePosition + rotation * (localPoints[index] - localPoints[0]);
                var snapped = ClampToFloorArea(snapshot, SnapToFloor(snapshot, worldPosition));
                bool grounded = Mathf.Abs(worldPosition.y - snapped.y) <= 0.15f;
                var (bottom, top) = BuildCapsulePoints(worldPosition);
                bool collisionFree = !HasBlockingCapsuleOverlap(bottom, top, DefaultAvatarRadius)
                    && !HasBlockingSnapshotOverlap(snapshot, string.Empty, bottom, top, DefaultAvatarRadius);
                if (!grounded)
                {
                    issues.Add(new ValidationIssueModel { code = "TRAJECTORY_NOT_GROUNDED", message = $"Trajectory sample {index} is not grounded." });
                    break;
                }
                if (!collisionFree)
                {
                    issues.Add(new ValidationIssueModel { code = "TRAJECTORY_COLLISION", message = $"Trajectory sample {index} overlaps scene geometry." });
                    break;
                }
            }

            return (
                "translational",
                issues.Count == 0,
                issues.Count == 0,
                sampleIndices.Count,
                issues);
        }

        private static List<int> BuildSampleIndices(int count, int targetCount)
        {
            var result = new SortedSet<int> { 0, count - 1, count / 2 };
            if (count <= 3)
            {
                return result.ToList();
            }

            int step = Mathf.Max(1, Mathf.FloorToInt((count - 1f) / Mathf.Max(1, targetCount - 1)));
            for (int i = step; i < count - 1 && result.Count < targetCount; i += step)
            {
                result.Add(i);
            }
            return result.ToList();
        }

        private ValidationArtifactModel CaptureView(string validatorContext, string label, Vector3 position, Vector3 rotationEuler, Vector3 lookAt)
        {
            Camera sourceCamera = Camera.main;
            bool createdTempCamera = false;
            Camera captureCamera = sourceCamera;
            if (captureCamera == null)
            {
                var go = new GameObject("ValidationCaptureCamera");
                captureCamera = go.AddComponent<Camera>();
                createdTempCamera = true;
            }
            else
            {
                var go = new GameObject("ValidationCaptureCamera");
                captureCamera = go.AddComponent<Camera>();
                captureCamera.CopyFrom(sourceCamera);
                createdTempCamera = true;
            }

            try
            {
                captureCamera.transform.position = position;
                captureCamera.transform.rotation = Quaternion.Euler(rotationEuler);
                captureCamera.transform.LookAt(lookAt);

                var rt = new RenderTexture(960, 540, 24);
                captureCamera.targetTexture = rt;
                var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                captureCamera.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                texture.Apply();

                string captureDir = Path.Combine(Application.persistentDataPath, "ValidationCaptures");
                Directory.CreateDirectory(captureDir);
                string artifactId = $"{validatorContext}_{label}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}";
                string filePath = Path.Combine(captureDir, $"{artifactId}.png");
                File.WriteAllBytes(filePath, texture.EncodeToPNG());

                return new ValidationArtifactModel
                {
                    artifact_id = artifactId,
                    artifact_type = "image/png",
                    file_path = filePath,
                    label = label,
                    camera_position = ToData(captureCamera.transform.position),
                    camera_rotation = ToData(captureCamera.transform.eulerAngles)
                };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SceneValidationService] Failed to capture validation view '{label}': {ex.Message}");
                return null;
            }
            finally
            {
                if (captureCamera != null)
                {
                    if (captureCamera.targetTexture != null)
                    {
                        RenderTexture.active = null;
                        captureCamera.targetTexture.Release();
                    }

                    if (createdTempCamera)
                    {
                        UnityEngine.Object.DestroyImmediate(captureCamera.gameObject);
                    }
                }
            }
        }

        private static Vector3 ResolveAvatarVisualForward(GameObject avatar)
        {
            if (avatar == null)
            {
                return Vector3.forward;
            }

            var controller = avatar.GetComponentInChildren<smplx.SmplxBodyAnimationController>(true);
            if (controller != null && controller.Root != null)
            {
                return controller.Root.forward;
            }

            return avatar.transform.forward;
        }

        private SceneObjectModel ResolveTarget(SceneSnapshot snapshot, string targetObjectId, string targetAlias)
        {
            if (!string.IsNullOrWhiteSpace(targetObjectId))
            {
                var byId = snapshot.objects.FirstOrDefault(obj => obj.id == targetObjectId);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (!string.IsNullOrWhiteSpace(targetAlias))
            {
                return snapshot.objects.FirstOrDefault(obj =>
                    string.Equals(obj.alias, targetAlias, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private static float EstimateObjectRadius(Vector3 targetSize)
        {
            return Mathf.Max(0.18f, Mathf.Max(targetSize.x, targetSize.z) * 0.5f);
        }

        private static float GetPreferredDistance(string taskHint, SceneObjectModel target)
        {
            return GetPreferredDistance(taskHint, ToVector3(target.bounds_size));
        }

        private static float GetPreferredDistance(string taskHint, Vector3 targetSize)
        {
            float baseDistance = EstimateObjectRadius(targetSize) + 0.55f;
            if (!string.IsNullOrWhiteSpace(taskHint) && taskHint.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return baseDistance + 0.35f;
            }
            return baseDistance;
        }

        private static float GetMinDistance(string taskHint, Vector3 targetSize)
        {
            return Mathf.Max(0.45f, EstimateObjectRadius(targetSize) + 0.2f);
        }

        private static float GetMaxDistance(string taskHint, Vector3 targetSize)
        {
            return GetPreferredDistance(taskHint, targetSize) + 0.75f;
        }

        private Vector3 SnapToFloor(SceneSnapshot snapshot, Vector3 rawPosition)
        {
            float floorY = float.NaN;
            foreach (var surface in snapshot.surfaces.Where(surface => surface.surface_type == "floor"))
            {
                var center = ToVector3(surface.center);
                if (float.IsNaN(floorY) || Mathf.Abs(center.y - rawPosition.y) < Mathf.Abs(floorY - rawPosition.y))
                {
                    floorY = center.y;
                }
            }

            if (!float.IsNaN(floorY))
            {
                rawPosition.y = floorY;
                return rawPosition;
            }

            var floorColliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(collider =>
                {
                    if (collider == null)
                    {
                        return false;
                    }

                    var lowerName = collider.gameObject.name.ToLowerInvariant();
                    return lowerName.Contains("floor") || lowerName.Contains("ground");
                })
                .OrderBy(collider =>
                {
                    var center = collider.bounds.center;
                    var dx = center.x - rawPosition.x;
                    var dz = center.z - rawPosition.z;
                    return dx * dx + dz * dz;
                })
                .ToList();

            if (floorColliders.Count > 0)
            {
                rawPosition.y = floorColliders[0].bounds.max.y;
                return rawPosition;
            }

            var rayOrigin = rawPosition + Vector3.up * DefaultFloorSnapDistance;
            var hits = Physics.RaycastAll(rayOrigin, Vector3.down, DefaultFloorSnapDistance * 2f)
                .Where(hit => hit.collider != null)
                .OrderBy(hit => hit.point.y)
                .ToList();

            if (hits.Count == 0)
            {
                return rawPosition;
            }

            var preferred = hits.FirstOrDefault(hit =>
            {
                var lowerName = hit.collider.gameObject.name.ToLowerInvariant();
                return lowerName.Contains("floor") || lowerName.Contains("ground");
            });

            if (preferred.collider == null)
            {
                preferred = hits[0];
            }

            rawPosition.y = preferred.point.y;
            return rawPosition;
        }

        private Vector3 ClampToFloorArea(SceneSnapshot snapshot, Vector3 rawPosition)
        {
            if (snapshot?.surfaces == null || snapshot.surfaces.Count == 0)
            {
                return rawPosition;
            }

            var floorSurfaces = snapshot.surfaces
                .Where(surface => surface != null && surface.surface_type == "floor" && surface.boundary_polygon != null && surface.boundary_polygon.Count >= 3)
                .ToList();

            if (floorSurfaces.Count == 0)
            {
                return rawPosition;
            }

            if (TryProjectOntoFloorSurface(floorSurfaces, rawPosition, requireContainment: true, out var contained))
            {
                return contained;
            }

            if (TryProjectOntoFloorSurface(floorSurfaces, rawPosition, requireContainment: false, out var clamped))
            {
                return clamped;
            }

            return rawPosition;
        }

        private bool IsWithinFloorFootprint(SceneSnapshot snapshot, Vector3 rawPosition)
        {
            if (snapshot?.surfaces == null)
            {
                return true;
            }

            var floorSurfaces = snapshot.surfaces
                .Where(surface => surface != null && surface.surface_type == "floor" && surface.boundary_polygon != null && surface.boundary_polygon.Count >= 3)
                .ToList();

            if (floorSurfaces.Count == 0)
            {
                return true;
            }

            return TryProjectOntoFloorSurface(floorSurfaces, rawPosition, requireContainment: true, out _);
        }

        private static bool TryProjectOntoFloorSurface(
            IEnumerable<SceneSurfaceModel> floorSurfaces,
            Vector3 rawPosition,
            bool requireContainment,
            out Vector3 projectedPosition)
        {
            projectedPosition = rawPosition;
            float bestDistance = float.PositiveInfinity;
            bool found = false;

            foreach (var surface in floorSurfaces)
            {
                var polygon = surface.boundary_polygon.Select(ToVector3).ToList();
                float minX = polygon.Min(p => p.x);
                float maxX = polygon.Max(p => p.x);
                float minZ = polygon.Min(p => p.z);
                float maxZ = polygon.Max(p => p.z);
                bool contains = rawPosition.x >= minX && rawPosition.x <= maxX && rawPosition.z >= minZ && rawPosition.z <= maxZ;
                if (requireContainment && !contains)
                {
                    continue;
                }

                var clamped = new Vector3(
                    Mathf.Clamp(rawPosition.x, minX, maxX),
                    ToVector3(surface.center).y,
                    Mathf.Clamp(rawPosition.z, minZ, maxZ));
                float distance = Vector3.SqrMagnitude(rawPosition - clamped);
                if (distance < bestDistance)
                {
                    projectedPosition = clamped;
                    bestDistance = distance;
                    found = true;
                }
            }

            return found;
        }

        private static bool CapsuleIntersectsBounds(Vector3 bottom, Vector3 top, float radius, Bounds bounds)
        {
            var closestToBottom = bounds.ClosestPoint(bottom);
            if ((closestToBottom - bottom).sqrMagnitude <= radius * radius)
            {
                return true;
            }
            var closestToTop = bounds.ClosestPoint(top);
            return (closestToTop - top).sqrMagnitude <= radius * radius;
        }

        private static bool HasBlockingCapsuleOverlap(Vector3 bottom, Vector3 top, float radius)
        {
            var overlaps = Physics.OverlapCapsule(bottom, top, radius);
            foreach (var collider in overlaps)
            {
                if (collider == null)
                {
                    continue;
                }

                var lowerName = collider.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("floor") || lowerName.Contains("ground"))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool HasBlockingSnapshotOverlap(SceneSnapshot snapshot, string excludedObjectId, Vector3 bottom, Vector3 top, float radius)
        {
            if (snapshot == null || snapshot.objects == null)
            {
                return false;
            }

            foreach (var sceneObject in snapshot.objects)
            {
                if (sceneObject == null || sceneObject.id == excludedObjectId || !IsBlockingSceneObject(sceneObject))
                {
                    continue;
                }

                var bounds = new Bounds(ToVector3(sceneObject.bounds_center), ToVector3(sceneObject.bounds_size));
                if (CapsuleIntersectsBounds(bottom, top, radius, bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBlockingSceneObject(SceneObjectModel sceneObject)
        {
            var identity = $"{sceneObject.id} {sceneObject.alias} {sceneObject.kind}".ToLowerInvariant();
            if (identity.Contains("floor") || identity.Contains("ground"))
            {
                return false;
            }

            if (sceneObject.tags != null && sceneObject.tags.Any(tag =>
                    string.Equals(tag, "floor", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tag, "ground", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var boundsSize = ToVector3(sceneObject.bounds_size);
            return boundsSize.sqrMagnitude > 1e-6f;
        }

        private static (Vector3 bottom, Vector3 top) BuildCapsulePoints(Vector3 center)
        {
            float halfHeight = DefaultAvatarHeight * 0.5f;
            var bottom = center + Vector3.up * DefaultAvatarRadius;
            var top = center + Vector3.up * Mathf.Max(DefaultAvatarRadius, DefaultAvatarHeight - DefaultAvatarRadius);
            if (top.y - bottom.y < DefaultAvatarRadius)
            {
                top = center + Vector3.up * (halfHeight + DefaultAvatarRadius);
            }
            return (bottom, top);
        }

        private static float ScoreValidation(AvatarPlacementValidationModel report)
        {
            float score = 0f;
            if (report.collision_free) score += 0.45f;
            if (report.grounded) score += 0.2f;
            if (report.endpoint_valid) score += 0.2f;
            if (report.trajectory_valid) score += 0.15f;
            score += report.facing_score * 0.1f;
            score -= Mathf.Min(0.4f, report.issues.Count * 0.08f);
            return (float)Math.Round(score, 4);
        }

        private static Vector3 GetFallbackPosePosition(SceneObjectModel target, string taskHint)
        {
            var targetCenter = ToVector3(target.bounds_center);
            return targetCenter + Vector3.back * GetPreferredDistance(taskHint, target);
        }

        private static Vector3 ComputeFacingRotation(Vector3 posePosition, Vector3 targetCenter)
        {
            var lookDir = targetCenter - posePosition;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude < 1e-4f)
            {
                lookDir = Vector3.forward;
            }
            return Quaternion.LookRotation(lookDir.normalized, Vector3.up).eulerAngles;
        }

        private static Vector3 ToVector3(Vector3Data data)
        {
            return new Vector3(data.x, data.y, data.z);
        }

        private static Vector3Data ToData(Vector3 v)
        {
            return new Vector3Data((float)Math.Round(v.x, 3), (float)Math.Round(v.y, 3), (float)Math.Round(v.z, 3));
        }
    }
}
