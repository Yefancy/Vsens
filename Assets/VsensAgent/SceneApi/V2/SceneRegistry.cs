using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sensor;
using UnityEngine;
using VsensAgent;
using VsensAgent.Core;

namespace VsensAgent.SceneApi.V2
{
    public class SceneRegistry : MonoBehaviour
    {
        [SerializeField] private string sceneId = "unity_scene";
        [SerializeField] private int sceneVersion = 1;

        private readonly Dictionary<string, ObjectDescriber> _idToDescriber = new Dictionary<string, ObjectDescriber>();
        private readonly Dictionary<string, string> _aliasToId = new Dictionary<string, string>();
        private int _mutationBatchDepth;
        private bool _mutationBatchDirty;
        private int _pendingMutationCount;
        private string _lastMutationSource = string.Empty;

        public int CurrentVersion => sceneVersion;
        public bool IsMutationBatchOpen => _mutationBatchDepth > 0;
        public int PendingMutationCount => _pendingMutationCount;
        public string LastMutationSource => _lastMutationSource;

        private void Awake()
        {
            ServiceLocator.Register<SceneRegistry>(this);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.IsRegistered<SceneRegistry>() && ServiceLocator.Get<SceneRegistry>() == this)
            {
                ServiceLocator.Unregister<SceneRegistry>();
            }
        }

        public SceneSnapshot BuildSnapshot(bool includeRelations = true)
        {
            _idToDescriber.Clear();
            _aliasToId.Clear();

            var snapshot = new SceneSnapshot
            {
                scene_id = sceneId,
                scene_version = sceneVersion,
                generated_at = DateTime.UtcNow.ToString("o")
            };

            var describers = FindObjectsByType<ObjectDescriber>(FindObjectsSortMode.None);
            var objects = new List<SceneObjectModel>(describers.Length);

            foreach (var describer in describers)
            {
                if (describer == null) continue;
                var obj = BuildObjectModel(describer);
                objects.Add(obj);
                _idToDescriber[obj.id] = describer;
                if (!string.IsNullOrWhiteSpace(obj.alias) && !_aliasToId.ContainsKey(obj.alias))
                {
                    _aliasToId[obj.alias] = obj.id;
                }
            }

            snapshot.objects = objects;
            snapshot.surfaces = BuildSurfaces(objects);
            snapshot.zones = BuildZones(objects);
            snapshot.relations = includeRelations ? BuildRelations(objects, snapshot.surfaces) : new List<SceneRelationModel>();
            return snapshot;
        }

        public bool TryResolveObject(string id, out ObjectDescriber describer)
        {
            if (_idToDescriber.Count == 0)
            {
                BuildSnapshot(includeRelations: false);
            }

            return _idToDescriber.TryGetValue(id, out describer);
        }

        public bool TryResolveIdByAlias(string alias, out string id)
        {
            if (_aliasToId.Count == 0)
            {
                BuildSnapshot(includeRelations: false);
            }

            return _aliasToId.TryGetValue(alias, out id);
        }

        public void IncrementVersion()
        {
            RegisterMutation("legacy.increment_version");
        }

        public void BeginMutationBatch(string source)
        {
            _mutationBatchDepth += 1;
            if (_mutationBatchDepth == 1)
            {
                _mutationBatchDirty = false;
                _pendingMutationCount = 0;
                _lastMutationSource = source ?? string.Empty;
            }
        }

        public void RegisterMutation(string source, string targetId = "", string actionType = "")
        {
            _lastMutationSource = BuildMutationLabel(source, targetId, actionType);

            if (_mutationBatchDepth > 0)
            {
                _mutationBatchDirty = true;
                _pendingMutationCount += 1;
                return;
            }

            sceneVersion += 1;
        }

        public void CommitMutationBatch()
        {
            if (_mutationBatchDepth == 0)
            {
                return;
            }

            _mutationBatchDepth -= 1;
            if (_mutationBatchDepth > 0)
            {
                return;
            }

            if (_mutationBatchDirty)
            {
                sceneVersion += 1;
            }

            _mutationBatchDirty = false;
            _pendingMutationCount = 0;
        }

        public void RollbackMutationBatch()
        {
            _mutationBatchDepth = 0;
            _mutationBatchDirty = false;
            _pendingMutationCount = 0;
        }

        private static string BuildMutationLabel(string source, string targetId, string actionType)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(source))
            {
                parts.Add(source);
            }
            if (!string.IsNullOrWhiteSpace(actionType))
            {
                parts.Add(actionType);
            }
            if (!string.IsNullOrWhiteSpace(targetId))
            {
                parts.Add(targetId);
            }

            return parts.Count > 0 ? string.Join(":", parts) : "mutation";
        }

        private SceneObjectModel BuildObjectModel(ObjectDescriber describer)
        {
            var go = describer.gameObject;
            var alias = describer.GetObjectName();
            var model = new SceneObjectModel
            {
                id = BuildStableId(go),
                alias = alias,
                kind = InferKind(describer),
                room_id = InferRoomId(go),
                position = ToData(go.transform.position),
                rotation = ToData(go.transform.eulerAngles),
                scale = ToData(go.transform.lossyScale),
                capabilities = InferCapabilities(describer),
                tags = InferTags(describer)
            };

            var box = go.GetComponent<BoxCollider>();
            if (box != null)
            {
                var boxData = BoxColliderData.FromBoxCollider(box, go.transform);
                model.bounds_center = ToData(boxData.position);
                model.bounds_size = ToData(boxData.size);
            }
            else
            {
                model.bounds_center = ToData(go.transform.position);
                model.bounds_size = new Vector3Data(0.1f, 0.1f, 0.1f);
            }

            if (go.TryGetComponent<IStateHolder>(out var stateHolder))
            {
                model.state["current"] = stateHolder.getCurrentState();
                model.state["available"] = stateHolder.getAvailableStates().ToArray();
            }

            if (go.TryGetComponent<VirtualSensor>(out var sensor))
            {
                model.state["sensor_type"] = sensor.SensorDefinition().getSensorName();
                model.state["show_preview"] = sensor.ShowPreview;
                model.state["show_graph"] = sensor.ShowGraph;
            }

            if (go.TryGetComponent<AvatarRuntimeState>(out var avatarState))
            {
                model.state["avatar_id"] = avatarState.avatarId;
                model.state["motion_id"] = avatarState.motionId;
                model.state["motion_name"] = avatarState.motionName;
                model.state["is_playing"] = avatarState.isPlaying;
                model.state["pose_authority"] = avatarState.poseAuthority;
            }

            return model;
        }

        private List<SceneSurfaceModel> BuildSurfaces(List<SceneObjectModel> objects)
        {
            var result = new List<SceneSurfaceModel>();
            foreach (var obj in objects)
            {
                if (!_idToDescriber.TryGetValue(obj.id, out var describer) || describer == null)
                {
                    continue;
                }

                var lowerAlias = (obj.alias ?? string.Empty).ToLowerInvariant();
                var isWall = lowerAlias.Contains("wall");
                var isFloor = lowerAlias.Contains("floor") || lowerAlias.Contains("ground");
                var isCeiling = lowerAlias.Contains("ceiling");

                if (!isWall && !isFloor && !isCeiling)
                {
                    continue;
                }

                var t = describer.transform;
                var size = new Vector3(obj.bounds_size.x, obj.bounds_size.y, obj.bounds_size.z);
                var center = new Vector3(obj.bounds_center.x, obj.bounds_center.y, obj.bounds_center.z);

                var surface = new SceneSurfaceModel
                {
                    surface_id = $"surf_{obj.id}",
                    parent_object_id = obj.id,
                    surface_type = isWall ? "wall" : (isFloor ? "floor" : "ceiling"),
                    center = ToData(center),
                    normal = ToData(isWall ? t.forward.normalized : (isCeiling ? Vector3.down : Vector3.up)),
                    mountable = isWall,
                    clearance_min = 0.05f,
                    tags = new List<string> { obj.room_id }
                };

                var normal = isWall ? t.forward.normalized : (isCeiling ? Vector3.down : Vector3.up);
                var up = isWall ? Vector3.up : t.forward.normalized;
                var right = Vector3.Cross(up, normal).normalized;
                if (right.sqrMagnitude < 1e-5f)
                {
                    right = t.right.normalized;
                }

                float halfW = isWall ? Mathf.Max(size.x, size.z) * 0.5f : size.x * 0.5f;
                float halfH = isWall ? size.y * 0.5f : size.z * 0.5f;

                var p1 = center - right * halfW - up * halfH;
                var p2 = center + right * halfW - up * halfH;
                var p3 = center + right * halfW + up * halfH;
                var p4 = center - right * halfW + up * halfH;
                surface.boundary_polygon.Add(ToData(p1));
                surface.boundary_polygon.Add(ToData(p2));
                surface.boundary_polygon.Add(ToData(p3));
                surface.boundary_polygon.Add(ToData(p4));

                result.Add(surface);
            }

            return result;
        }

        private List<SceneZoneModel> BuildZones(List<SceneObjectModel> objects)
        {
            var byRoom = objects.GroupBy(o => o.room_id ?? "room_unknown");
            var zones = new List<SceneZoneModel>();
            foreach (var roomGroup in byRoom)
            {
                var centers = roomGroup.Select(o => new Vector3(o.bounds_center.x, o.bounds_center.y, o.bounds_center.z)).ToList();
                if (centers.Count == 0) continue;

                var min = centers[0];
                var max = centers[0];
                foreach (var c in centers)
                {
                    min = Vector3.Min(min, c);
                    max = Vector3.Max(max, c);
                }

                zones.Add(new SceneZoneModel
                {
                    zone_id = $"zone_{roomGroup.Key}",
                    name = roomGroup.Key,
                    type = "semantic",
                    center = ToData((min + max) * 0.5f),
                    size = ToData(max - min)
                });
            }

            return zones;
        }

        private List<SceneRelationModel> BuildRelations(List<SceneObjectModel> objects, List<SceneSurfaceModel> surfaces)
        {
            var relations = new List<SceneRelationModel>();
            const float nearThreshold = 2.5f;

            for (int i = 0; i < objects.Count; i++)
            {
                var a = objects[i];
                var pa = new Vector3(a.bounds_center.x, a.bounds_center.y, a.bounds_center.z);
                for (int j = i + 1; j < objects.Count; j++)
                {
                    var b = objects[j];
                    var pb = new Vector3(b.bounds_center.x, b.bounds_center.y, b.bounds_center.z);
                    var dist = Vector3.Distance(pa, pb);
                    if (dist > nearThreshold) continue;

                    relations.Add(new SceneRelationModel
                    {
                        type = "near",
                        from_id = a.id,
                        to_id = b.id,
                        metadata = new Dictionary<string, object> { ["distance"] = Math.Round(dist, 3) }
                    });
                }
            }

            foreach (var obj in objects)
            {
                var p = new Vector3(obj.bounds_center.x, obj.bounds_center.y, obj.bounds_center.z);
                foreach (var surface in surfaces.Where(s => s.mountable))
                {
                    var center = new Vector3(surface.center.x, surface.center.y, surface.center.z);
                    var dist = Vector3.Distance(center, p);
                    if (dist > 3.5f) continue;

                    relations.Add(new SceneRelationModel
                    {
                        type = "can_mount_on",
                        from_id = obj.id,
                        to_id = surface.surface_id,
                        metadata = new Dictionary<string, object> { ["distance"] = Math.Round(dist, 3) }
                    });
                }
            }

            return relations;
        }

        private List<string> InferCapabilities(ObjectDescriber describer)
        {
            var caps = new HashSet<string>();
            caps.Add("highlight");
            if (describer.HasProperty("with_state")) caps.Add("set_state");
            if (describer.HasProperty("movable")) caps.Add("set_transform");
            if (describer.HasProperty("sensor")) caps.Add("set_sensor");
            if (describer.HasProperty("avatar"))
            {
                caps.Add("spawn_avatar");
                caps.Add("remove_avatar");
                caps.Add("set_avatar_transform");
                caps.Add("load_avatar_motion");
                caps.Add("play_avatar_motion");
                caps.Add("pause_avatar_motion");
                caps.Add("stop_avatar_motion");
                caps.Add("clear_avatar_motion");
            }
            return caps.ToList();
        }

        private List<string> InferTags(ObjectDescriber describer)
        {
            var tags = new HashSet<string>(describer.GetProperties());
            tags.Add(InferKind(describer));
            return tags.ToList();
        }

        private string InferKind(ObjectDescriber describer)
        {
            var go = describer.gameObject;
            if (go.GetComponent<VirtualSensor>() != null) return "sensor";
            if (describer.HasProperty("avatar")) return "avatar";
            if (go.GetComponent<IStateHolder>() != null) return "state_object";
            if (describer.HasProperty("movable")) return "movable";

            var alias = (describer.GetObjectName() ?? string.Empty).ToLowerInvariant();
            if (alias.Contains("wall") || alias.Contains("floor") || alias.Contains("ceiling"))
            {
                return "structure";
            }

            return "structure";
        }

        private string InferRoomId(GameObject go)
        {
            var path = BuildTransformPath(go.transform).ToLowerInvariant();
            if (path.Contains("kitchen")) return "room_kitchen";
            if (path.Contains("living")) return "room_living";
            if (path.Contains("bed")) return "room_bedroom";
            return "room_default";
        }

        private static Vector3Data ToData(Vector3 v)
        {
            return new Vector3Data((float)Math.Round(v.x, 3), (float)Math.Round(v.y, 3), (float)Math.Round(v.z, 3));
        }

        private string BuildStableId(GameObject go)
        {
            string path = BuildTransformPath(go.transform);
            uint hash = Fnv1a(path);
            return $"obj_{hash:x8}";
        }

        private static string BuildTransformPath(Transform t)
        {
            if (t == null) return string.Empty;
            var sb = new StringBuilder();
            var stack = new Stack<string>();
            var current = t;
            while (current != null)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            while (stack.Count > 0)
            {
                if (sb.Length > 0) sb.Append('/');
                sb.Append(stack.Pop());
            }

            return sb.ToString();
        }

        private static uint Fnv1a(string input)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;
                uint hash = offset;
                foreach (char c in input)
                {
                    hash ^= c;
                    hash *= prime;
                }
                return hash;
            }
        }
    }
}
