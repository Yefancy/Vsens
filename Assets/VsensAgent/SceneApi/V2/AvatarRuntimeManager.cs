using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VsensAgent.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VsensAgent.SceneApi.V2
{
    public abstract class AvatarPlaybackDriver : MonoBehaviour
    {
        public string LoadedMotionId { get; protected set; } = string.Empty;
        public string LoadedMotionName { get; protected set; } = string.Empty;
        public string LastMotionJson { get; protected set; } = string.Empty;
        public bool HasLoadedMotion { get; protected set; }
        public bool IsPlaying { get; protected set; }
        public float PlaybackSpeed { get; protected set; } = 1f;
        public bool Looping { get; protected set; } = true;

        public abstract bool TryLoadMotion(string motionId, string motionName, string motionJson, out string error);

        public virtual bool TryPlay(float speed, bool loop, out string error)
        {
            if (!HasLoadedMotion)
            {
                error = "No motion loaded on avatar.";
                return false;
            }

            PlaybackSpeed = speed;
            Looping = loop;
            IsPlaying = true;
            error = null;
            return true;
        }

        public virtual bool TryPause(out string error)
        {
            IsPlaying = false;
            error = null;
            return true;
        }

        public virtual bool TryStop(out string error)
        {
            IsPlaying = false;
            error = null;
            return true;
        }

        public virtual bool TryClear(out string error)
        {
            LoadedMotionId = string.Empty;
            LoadedMotionName = string.Empty;
            LastMotionJson = string.Empty;
            HasLoadedMotion = false;
            IsPlaying = false;
            error = null;
            return true;
        }
    }

    [RequireComponent(typeof(smplx.SmplxBodyAnimationController))]
    public class SmplxAvatarPlaybackDriver : AvatarPlaybackDriver
    {
        [SerializeField] private smplx.SmplxBodyAnimationController controller;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<smplx.SmplxBodyAnimationController>();
            }

            if (controller == null)
            {
                controller = GetComponentInChildren<smplx.SmplxBodyAnimationController>(true);
            }
        }

        public override bool TryLoadMotion(string motionId, string motionName, string motionJson, out string error)
        {
            if (controller == null)
            {
                controller = GetComponentInChildren<smplx.SmplxBodyAnimationController>(true);
            }

            if (controller == null)
            {
                error = "SmplxBodyAnimationController is missing.";
                return false;
            }

            EnsureControllerPrepared();
            controller.setAnimation(motionName, motionJson);
            controller.globalTranslation = true;
            controller.time = 0f;
            controller.isPlaying = false;

            LoadedMotionId = motionId ?? string.Empty;
            LoadedMotionName = motionName ?? string.Empty;
            LastMotionJson = motionJson ?? string.Empty;
            HasLoadedMotion = true;
            IsPlaying = false;
            error = null;
            return true;
        }

        private void EnsureControllerPrepared()
        {
            if (controller == null)
            {
                return;
            }

            if (controller.Root != null && controller.Bones != null && controller.Bones.Length > 0)
            {
                return;
            }

            controller.PrepareModel();
        }

        public override bool TryPlay(float speed, bool loop, out string error)
        {
            if (!base.TryPlay(speed, loop, out error))
            {
                return false;
            }

            controller.speed = speed;
            controller.isPlaying = true;
            return true;
        }

        public override bool TryPause(out string error)
        {
            controller.isPlaying = false;
            return base.TryPause(out error);
        }

        public override bool TryStop(out string error)
        {
            controller.isPlaying = false;
            controller.time = 0f;
            if (controller.hasAnimation)
            {
                controller.PlayAnimationToTime();
            }
            return base.TryStop(out error);
        }

        public override bool TryClear(out string error)
        {
            controller.removeAnimation();
            controller.time = 0f;
            controller.isPlaying = false;
            return base.TryClear(out error);
        }
    }

    public class AvatarRuntimeState : MonoBehaviour
    {
        public string avatarId = string.Empty;
        public string motionId = string.Empty;
        public string motionName = string.Empty;
        public bool isPlaying;
        public string poseAuthority = "agent";
    }

    public class AvatarObjectDescriber : global::ObjectDescriber
    {
        public override HashSet<string> GetProperties()
        {
            var properties = base.GetProperties();
            properties.Add("movable");
            properties.Add("avatar");
            return properties;
        }
    }

    public class AvatarRuntimeManager : MonoBehaviour
    {
        private const string DefaultAvatarId = "avatar_main";
        private const string MalePrefabKey = "smplx_male";
        private const string DefaultMalePrefabPath = "Assets/smplx/male.prefab";
        private static readonly Quaternion SmplxVisualYawOffset = Quaternion.Euler(0f, 180f, 0f);

        [SerializeField] private GameObject malePrefab;
        [SerializeField] private Transform avatarParent;

        private GameObject _avatarObject;
        private AvatarPlaybackDriver _playbackDriver;
        private AvatarRuntimeState _runtimeState;
        private readonly Dictionary<string, AvatarMotionQueryModel> _motionCatalog = new Dictionary<string, AvatarMotionQueryModel>();
        private string _poseAuthority = "agent";

        private void Awake()
        {
            ServiceLocator.Register<AvatarRuntimeManager>(this);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.IsRegistered<AvatarRuntimeManager>() && ServiceLocator.Get<AvatarRuntimeManager>() == this)
            {
                ServiceLocator.Unregister<AvatarRuntimeManager>();
            }
        }

        public void ConfigureDefaultPrefab(GameObject prefab)
        {
            malePrefab = prefab;
        }

        public bool TrySpawnAvatar(string avatarId, string prefabKey, Vector3 position, Vector3 rotationEuler, out string error)
        {
            avatarId = string.IsNullOrWhiteSpace(avatarId) ? DefaultAvatarId : avatarId;
            prefabKey = string.IsNullOrWhiteSpace(prefabKey) ? MalePrefabKey : prefabKey;
            position = SnapPositionToFloor(position);
            var appliedRotation = ApplyAvatarRotationOffset(rotationEuler);

            if (_avatarObject != null)
            {
                if (_avatarObject.name != avatarId)
                {
                    error = "Only a single managed avatar is supported in v1.";
                    return false;
                }

                _avatarObject.transform.position = position;
                _avatarObject.transform.rotation = appliedRotation;
                _poseAuthority = "agent";
                UpdateRuntimeState();
                error = null;
                return true;
            }

            var prefab = ResolvePrefab(prefabKey);
            if (prefab == null)
            {
                error = $"Avatar prefab '{prefabKey}' could not be resolved.";
                return false;
            }

            _avatarObject = Instantiate(prefab, position, appliedRotation, avatarParent);
            _avatarObject.name = avatarId;
            _playbackDriver = _avatarObject.GetComponent<AvatarPlaybackDriver>();
            if (_playbackDriver == null)
            {
                _playbackDriver = _avatarObject.AddComponent<SmplxAvatarPlaybackDriver>();
            }

            if (_avatarObject.GetComponent<AvatarObjectDescriber>() == null)
            {
                _avatarObject.AddComponent<AvatarObjectDescriber>();
            }

            _runtimeState = _avatarObject.GetComponent<AvatarRuntimeState>();
            if (_runtimeState == null)
            {
                _runtimeState = _avatarObject.AddComponent<AvatarRuntimeState>();
            }

            _poseAuthority = "agent";
            UpdateRuntimeState();
            error = null;
            return true;
        }

        public bool TryRemoveAvatar(string avatarId, out string error)
        {
            if (!HasMatchingAvatar(avatarId))
            {
                error = "Managed avatar not found.";
                return false;
            }

            var avatar = _avatarObject;
            _avatarObject = null;
            _playbackDriver = null;
            _runtimeState = null;

            if (Application.isPlaying)
            {
                Destroy(avatar);
            }
            else
            {
                DestroyImmediate(avatar);
            }

            error = null;
            return true;
        }

        public bool TrySetAvatarTransform(string avatarId, Vector3? position, Vector3? rotationEuler, Vector3? scale, out string error)
        {
            if (!HasMatchingAvatar(avatarId))
            {
                error = "Managed avatar not found.";
                return false;
            }

            if (position.HasValue)
            {
                _avatarObject.transform.position = SnapPositionToFloor(position.Value);
            }
            if (rotationEuler.HasValue)
            {
                _avatarObject.transform.rotation = ApplyAvatarRotationOffset(rotationEuler.Value);
            }
            if (scale.HasValue)
            {
                _avatarObject.transform.localScale = scale.Value;
            }

            _poseAuthority = "agent";
            UpdateRuntimeState();
            error = null;
            return true;
        }

        public bool TrySetManualAvatarPose(string avatarId, Vector3? position, float? yawDegrees, out string error)
        {
            if (!HasMatchingAvatar(avatarId))
            {
                error = "Managed avatar not found.";
                return false;
            }

            if (position.HasValue)
            {
                _avatarObject.transform.position = SnapPositionToFloor(position.Value);
            }

            if (yawDegrees.HasValue)
            {
                var logicalEuler = GetLogicalRotationEuler(_avatarObject.transform.rotation);
                logicalEuler.y = yawDegrees.Value;
                _avatarObject.transform.rotation = ApplyAvatarRotationOffset(logicalEuler);
            }

            _poseAuthority = "manual";
            UpdateRuntimeState();
            error = null;
            return true;
        }

        public bool TryLoadAvatarMotion(
            string avatarId,
            string motionId,
            string motionName,
            string motionJson,
            string sourceText,
            out string error)
        {
            if (!EnsureAvatarForMotion(avatarId, out error))
            {
                return false;
            }

            if (_playbackDriver.IsPlaying)
            {
                _playbackDriver.TryStop(out _);
            }

            if (!_playbackDriver.TryLoadMotion(motionId, motionName, motionJson, out error))
            {
                return false;
            }

            _motionCatalog[motionId] = new AvatarMotionQueryModel
            {
                motion_id = motionId,
                motion_name = motionName,
                source_text = sourceText,
                has_inline_json = !string.IsNullOrWhiteSpace(motionJson),
                loaded_to_avatar_id = avatarId,
            };

            UpdateRuntimeState();
            return true;
        }

        public bool TryPlayAvatarMotion(string avatarId, float speed, bool loop, out string error)
        {
            if (!EnsureAvatarForMotion(avatarId, out error))
            {
                return false;
            }

            var ok = _playbackDriver.TryPlay(speed, loop, out error);
            UpdateRuntimeState();
            return ok;
        }

        public bool TryPauseAvatarMotion(string avatarId, out string error)
        {
            if (!HasMatchingAvatar(avatarId))
            {
                error = "Managed avatar not found.";
                return false;
            }

            var ok = _playbackDriver.TryPause(out error);
            UpdateRuntimeState();
            return ok;
        }

        public bool TryStopAvatarMotion(string avatarId, out string error)
        {
            if (!HasMatchingAvatar(avatarId))
            {
                error = "Managed avatar not found.";
                return false;
            }

            var ok = _playbackDriver.TryStop(out error);
            UpdateRuntimeState();
            return ok;
        }

        public bool TryClearAvatarMotion(string avatarId, out string error)
        {
            if (!HasMatchingAvatar(avatarId))
            {
                error = "Managed avatar not found.";
                return false;
            }

            var ok = _playbackDriver.TryClear(out error);
            UpdateRuntimeState();
            return ok;
        }

        public List<AvatarQueryModel> GetAvatarQueryModels(SceneRegistry registry)
        {
            var result = new List<AvatarQueryModel>();
            if (_avatarObject == null)
            {
                return result;
            }

            string objectId = string.Empty;
            if (registry != null)
            {
                registry.BuildSnapshot(includeRelations: false);
                registry.TryResolveIdByAlias(_avatarObject.name, out objectId);
            }

            result.Add(new AvatarQueryModel
            {
                avatar_id = _avatarObject.name,
                object_id = objectId,
                prefab_key = MalePrefabKey,
                position = new Vector3Data(_avatarObject.transform.position.x, _avatarObject.transform.position.y, _avatarObject.transform.position.z),
                rotation = ToData(GetLogicalRotationEuler(_avatarObject.transform.rotation)),
                motion_id = _playbackDriver != null ? _playbackDriver.LoadedMotionId : string.Empty,
                motion_name = _playbackDriver != null ? _playbackDriver.LoadedMotionName : string.Empty,
                is_playing = _playbackDriver != null && _playbackDriver.IsPlaying,
                pose_authority = _poseAuthority,
            });

            return result;
        }

        public List<AvatarMotionQueryModel> GetMotionQueryModels()
        {
            return new List<AvatarMotionQueryModel>(_motionCatalog.Values);
        }

        public GameObject GetManagedAvatarObject()
        {
            return _avatarObject;
        }

        private bool EnsureAvatarForMotion(string avatarId, out string error)
        {
            if (_avatarObject == null)
            {
                return TrySpawnAvatar(
                    string.IsNullOrWhiteSpace(avatarId) ? DefaultAvatarId : avatarId,
                    MalePrefabKey,
                    Vector3.zero,
                    Vector3.zero,
                    out error
                );
            }

            if (!HasMatchingAvatar(avatarId))
            {
                error = "Requested avatar does not match the managed avatar.";
                return false;
            }

            error = null;
            return true;
        }

        private bool HasMatchingAvatar(string avatarId)
        {
            if (_avatarObject == null)
            {
                return false;
            }

            var normalized = string.IsNullOrWhiteSpace(avatarId) ? DefaultAvatarId : avatarId;
            return _avatarObject.name == normalized;
        }

        private GameObject ResolvePrefab(string prefabKey)
        {
            if (prefabKey != MalePrefabKey)
            {
                return null;
            }

            if (malePrefab != null)
            {
                return malePrefab;
            }

#if UNITY_EDITOR
            malePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMalePrefabPath);
#endif
            return malePrefab;
        }

        private void UpdateRuntimeState()
        {
            if (_runtimeState == null || _avatarObject == null)
            {
                return;
            }

            _runtimeState.avatarId = _avatarObject.name;
            _runtimeState.motionId = _playbackDriver != null ? _playbackDriver.LoadedMotionId : string.Empty;
            _runtimeState.motionName = _playbackDriver != null ? _playbackDriver.LoadedMotionName : string.Empty;
            _runtimeState.isPlaying = _playbackDriver != null && _playbackDriver.IsPlaying;
            _runtimeState.poseAuthority = _poseAuthority;
        }

        public static Quaternion ApplyAvatarRotationOffset(Vector3 logicalRotationEuler)
        {
            return Quaternion.Euler(logicalRotationEuler) * SmplxVisualYawOffset;
        }

        public static Quaternion GetLogicalRotation(Quaternion appliedRotation)
        {
            return appliedRotation * Quaternion.Inverse(SmplxVisualYawOffset);
        }

        public static Vector3 GetLogicalRotationEuler(Quaternion appliedRotation)
        {
            return GetLogicalRotation(appliedRotation).eulerAngles;
        }

        public static Vector3 GetLogicalForward(Quaternion appliedRotation)
        {
            return GetLogicalRotation(appliedRotation) * Vector3.forward;
        }

        private static Vector3Data ToData(Vector3 value)
        {
            return new Vector3Data(value.x, value.y, value.z);
        }

        private static Vector3 SnapPositionToFloor(Vector3 rawPosition)
        {
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
                    var bounds = collider.bounds;
                    var center = bounds.center;
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

            var rayOrigin = rawPosition + Vector3.up * 5f;
            var hits = Physics.RaycastAll(rayOrigin, Vector3.down, 20f)
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
    }
}
