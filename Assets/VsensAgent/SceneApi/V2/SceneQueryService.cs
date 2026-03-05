using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VsensAgent.SceneApi.V2
{
    public class SceneQueryService
    {
        private readonly SceneRegistry _registry;

        public SceneQueryService(SceneRegistry registry)
        {
            _registry = registry;
        }

        public object QuerySummary(int? clientVersion)
        {
            var snapshot = _registry.BuildSnapshot(includeRelations: false);
            var changed = clientVersion.HasValue && clientVersion.Value != snapshot.scene_version;
            return new
            {
                type = "scene.query_response",
                method = "scene.query_summary",
                scene_id = snapshot.scene_id,
                scene_version = snapshot.scene_version,
                counts = new
                {
                    objects = snapshot.objects.Count,
                    surfaces = snapshot.surfaces.Count,
                    zones = snapshot.zones.Count,
                    relations = snapshot.relations.Count
                },
                changed
            };
        }

        public object QueryObjects(List<string> ids, List<string> aliases, List<string> tags, string zoneId)
        {
            var snapshot = _registry.BuildSnapshot(includeRelations: false);
            var result = snapshot.objects.AsEnumerable();

            if (ids != null && ids.Count > 0)
            {
                var idSet = new HashSet<string>(ids);
                result = result.Where(o => idSet.Contains(o.id));
            }

            if (aliases != null && aliases.Count > 0)
            {
                var aliasSet = new HashSet<string>(aliases);
                result = result.Where(o => aliasSet.Contains(o.alias));
            }

            if (tags != null && tags.Count > 0)
            {
                var tagSet = new HashSet<string>(tags);
                result = result.Where(o => o.tags.Any(tagSet.Contains));
            }

            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                var zone = snapshot.zones.FirstOrDefault(z => z.zone_id == zoneId);
                if (zone != null)
                {
                    var c = ToVector3(zone.center);
                    var s = ToVector3(zone.size) * 0.5f;
                    result = result.Where(o =>
                    {
                        var p = ToVector3(o.bounds_center);
                        return Mathf.Abs(p.x - c.x) <= s.x && Mathf.Abs(p.y - c.y) <= s.y && Mathf.Abs(p.z - c.z) <= s.z;
                    });
                }
                else
                {
                    result = Enumerable.Empty<SceneObjectModel>();
                }
            }

            return new
            {
                type = "scene.query_response",
                method = "scene.query_objects",
                scene_version = snapshot.scene_version,
                objects = result.ToList()
            };
        }

        public object QueryRelations(string objectId, List<string> relationTypes, float? radius)
        {
            var snapshot = _registry.BuildSnapshot(includeRelations: true);
            var rels = snapshot.relations.Where(r => r.from_id == objectId || r.to_id == objectId);

            if (relationTypes != null && relationTypes.Count > 0)
            {
                var typeSet = new HashSet<string>(relationTypes);
                rels = rels.Where(r => typeSet.Contains(r.type));
            }

            if (radius.HasValue)
            {
                rels = rels.Where(r =>
                {
                    if (!r.metadata.TryGetValue("distance", out var dObj)) return true;
                    return Convert.ToSingle(dObj) <= radius.Value;
                });
            }

            return new
            {
                type = "scene.query_response",
                method = "scene.query_relations",
                scene_version = snapshot.scene_version,
                relations = rels.ToList()
            };
        }

        public object QuerySurfaces(string nearObjectId, string zoneId, bool mountableOnly)
        {
            var snapshot = _registry.BuildSnapshot(includeRelations: false);
            IEnumerable<SceneSurfaceModel> surfaces = snapshot.surfaces;

            if (mountableOnly)
            {
                surfaces = surfaces.Where(s => s.mountable);
            }

            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                var zone = snapshot.zones.FirstOrDefault(z => z.zone_id == zoneId);
                if (zone != null)
                {
                    var c = ToVector3(zone.center);
                    var s = ToVector3(zone.size) * 0.5f;
                    surfaces = surfaces.Where(surface =>
                    {
                        var p = ToVector3(surface.center);
                        return Mathf.Abs(p.x - c.x) <= s.x && Mathf.Abs(p.y - c.y) <= s.y && Mathf.Abs(p.z - c.z) <= s.z;
                    });
                }
                else
                {
                    surfaces = Enumerable.Empty<SceneSurfaceModel>();
                }
            }

            if (!string.IsNullOrWhiteSpace(nearObjectId))
            {
                var obj = snapshot.objects.FirstOrDefault(o => o.id == nearObjectId);
                if (obj != null)
                {
                    var center = ToVector3(obj.bounds_center);
                    surfaces = surfaces.Where(s => Vector3.Distance(ToVector3(s.center), center) <= 3.5f);
                }
            }

            return new
            {
                type = "scene.query_response",
                method = "scene.query_surfaces",
                scene_version = snapshot.scene_version,
                surfaces = surfaces.ToList()
            };
        }

        public object FindSensorPlacements(string sensorType, List<string> targetIds, PlacementConstraints constraints)
        {
            var snapshot = _registry.BuildSnapshot(includeRelations: false);
            var targets = snapshot.objects.Where(o => targetIds.Contains(o.id)).ToList();
            var surfaces = snapshot.surfaces.Where(s => s.mountable).ToList();

            var candidates = new List<PlacementCandidate>();
            int idx = 0;
            foreach (var target in targets)
            {
                var targetCenter = ToVector3(target.bounds_center);
                foreach (var surface in surfaces)
                {
                    var candidate = BuildCandidate(idx++, targetCenter, surface, constraints);
                    if (candidate == null) continue;
                    candidates.Add(candidate);
                }
            }

            var ordered = candidates
                .OrderByDescending(c => c.score)
                .ThenBy(c => c.occlusion_risk)
                .ThenBy(c => c.distance_penalty)
                .ThenBy(c => c.candidate_id)
                .Take(Mathf.Max(1, constraints.max_candidates))
                .ToList();

            return new
            {
                type = "scene.query_response",
                method = "scene.find_sensor_placements",
                scene_version = snapshot.scene_version,
                candidates = ordered,
                scoring = new
                {
                    coverage_weight = constraints.coverage_weight,
                    occlusion_weight = constraints.occlusion_weight,
                    distance_weight = constraints.distance_weight
                }
            };
        }

        public object ValidatePlacement(PlacementCandidate candidate, float minCoverage)
        {
            var valid = candidate != null && candidate.collision_free && candidate.coverage_score >= minCoverage;
            var reasons = new List<string>();
            if (candidate == null)
            {
                reasons.Add("candidate_missing");
            }
            else
            {
                if (!candidate.collision_free) reasons.Add(SceneApiErrorCodes.PLACEMENT_COLLISION);
                if (candidate.coverage_score < minCoverage) reasons.Add(SceneApiErrorCodes.COVERAGE_INSUFFICIENT);
            }

            return new
            {
                type = "scene.query_response",
                method = "scene.validate_placement",
                scene_version = _registry.CurrentVersion,
                valid,
                reasons,
                coverage_report = new { coverage = candidate?.coverage_score ?? 0f, required = minCoverage },
                collision_report = new { collision_free = candidate?.collision_free ?? false }
            };
        }

        private PlacementCandidate BuildCandidate(int idx, Vector3 targetCenter, SceneSurfaceModel surface, PlacementConstraints constraints)
        {
            var center = ToVector3(surface.center);
            var normal = ToVector3(surface.normal).normalized;
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.forward;

            if (surface.boundary_polygon == null || surface.boundary_polygon.Count < 4)
            {
                return null;
            }

            var p0 = ToVector3(surface.boundary_polygon[0]);
            var p1 = ToVector3(surface.boundary_polygon[1]);
            var p3 = ToVector3(surface.boundary_polygon[3]);
            var right = (p1 - p0).normalized;
            var up = (p3 - p0).normalized;
            var halfW = Vector3.Distance(p0, p1) * 0.5f;
            var halfH = Vector3.Distance(p0, p3) * 0.5f;

            var projected = targetCenter - Vector3.Dot(targetCenter - center, normal) * normal;
            var localX = Mathf.Clamp(Vector3.Dot(projected - center, right), -halfW, halfW);
            var localY = Mathf.Clamp(Vector3.Dot(projected - center, up), -halfH, halfH);
            var pos = center + right * localX + up * localY + normal * Mathf.Max(0.01f, surface.clearance_min);

            if (constraints.min_height > 0 && pos.y < constraints.min_height) return null;
            if (constraints.max_height > 0 && pos.y > constraints.max_height) return null;

            var lookDir = (targetCenter - pos).normalized;
            if (lookDir.sqrMagnitude < 1e-6f) lookDir = -normal;
            var rot = Quaternion.LookRotation(lookDir, Vector3.up).eulerAngles;

            var dist = Vector3.Distance(pos, targetCenter);
            if (constraints.max_distance_to_target > 0 && dist > constraints.max_distance_to_target)
            {
                return null;
            }

            float effectiveRange = Mathf.Max(0.5f, constraints.sensor_range_hint);
            float coverage = Mathf.Clamp01(1f - dist / effectiveRange);
            if (coverage < constraints.min_coverage)
            {
                return null;
            }

            bool collisionFree = !Physics.CheckSphere(pos, Mathf.Max(0.02f, constraints.sensor_radius_hint));
            float occlusionRisk = 0f;
            if (Physics.Linecast(pos, targetCenter, out _))
            {
                occlusionRisk = 0.35f;
            }

            float distancePenalty = Mathf.Clamp01(dist / Mathf.Max(0.01f, constraints.max_distance_to_target <= 0 ? effectiveRange : constraints.max_distance_to_target));
            float score = constraints.coverage_weight * coverage
                          - constraints.occlusion_weight * occlusionRisk
                          - constraints.distance_weight * distancePenalty;

            return new PlacementCandidate
            {
                candidate_id = $"cand_{idx:D4}",
                surface_id = surface.surface_id,
                position = ToData(pos),
                rotation = ToData(rot),
                score = (float)Math.Round(score, 4),
                coverage_score = (float)Math.Round(coverage, 4),
                occlusion_risk = (float)Math.Round(occlusionRisk, 4),
                distance_penalty = (float)Math.Round(distancePenalty, 4),
                collision_free = collisionFree,
                explanations = new List<string>
                {
                    "Mounted on surface boundary",
                    "Facing target center",
                    collisionFree ? "No overlap in quick collision check" : "Potential collision detected"
                }
            };
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

    [Serializable]
    public class PlacementConstraints
    {
        public float min_height;
        public float max_height;
        public float min_coverage = 0.8f;
        public float max_distance_to_target = 3.0f;
        public float sensor_range_hint = 3.0f;
        public float sensor_radius_hint = 0.06f;
        public float coverage_weight = 0.7f;
        public float occlusion_weight = 0.2f;
        public float distance_weight = 0.1f;
        public int max_candidates = 8;
    }
}
