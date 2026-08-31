using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metin2Dev.Gameplay
{
    /// <summary>
    /// Resolves gameplay positions against the imported Metin2 terrain instead of
    /// arbitrary scene colliders. This keeps spawns off roofs, water and steep cliffs.
    /// </summary>
    public static class Metin2WorldSurface
    {
        const float WaterClearance = 0.08f;
        static readonly List<Terrain> terrains = new List<Terrain>();
        static readonly List<Collider> waterColliders = new List<Collider>();
        static int cachedSceneHandle = int.MinValue;

        public static void Refresh()
        {
            cachedSceneHandle = int.MinValue;
            EnsureCache();
        }

        public static bool TryGetTerrainBounds(out Bounds bounds)
        {
            EnsureCache();
            bounds = default;
            bool found = false;
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 size = terrain.terrainData.size;
                Bounds current = new Bounds(terrain.transform.position + size * 0.5f, size);
                if (!found) { bounds = current; found = true; }
                else bounds.Encapsulate(current);
            }
            return found;
        }

        public static bool TryGetPlacementBounds(out Bounds bounds)
        {
            if (TryGetTerrainBounds(out bounds)) return true;
            EnsureCache();
            Scene scene = SceneManager.GetActiveScene();
            bool found = false;
            foreach (Collider collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (collider == null || collider.gameObject.scene != scene || collider.isTrigger ||
                    IsWater(collider.transform) || IsRuntimeEntity(collider.transform)) continue;
                Bounds current = collider.bounds;
                if (current.size.x < 0.05f || current.size.z < 0.05f || current.size.x > 10000f || current.size.z > 10000f) continue;
                if (!found) { bounds = current; found = true; }
                else bounds.Encapsulate(current);
            }
            return found;
        }

        public static bool TryFindSafePosition(Vector3 desired, float maxSlope, float searchRadius, int seed, out Vector3 grounded)
        {
            EnsureCache();
            if (TrySample(desired, maxSlope, out grounded)) return true;

            // Only move a source spawn when its exact point is unsafe. The search is
            // deterministic so a panel refresh cannot shuffle the live world.
            const int directions = 16;
            const int rings = 6;
            float radiusLimit = Mathf.Max(2f, searchRadius);
            float phase = Deterministic01(seed) * Mathf.PI * 2f;
            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = radiusLimit * ring / rings;
                for (int index = 0; index < directions; index++)
                {
                    float angle = phase + index * Mathf.PI * 2f / directions;
                    Vector3 candidate = desired + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (TrySample(candidate, maxSlope, out grounded)) return true;
                }
            }
            grounded = default;
            return false;
        }

        public static bool TrySample(Vector3 desired, float maxSlope, out Vector3 grounded)
        {
            EnsureCache();
            foreach (Terrain terrain in terrains)
            {
                if (!ContainsXZ(terrain, desired.x, desired.z)) continue;
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                float normalizedX = Mathf.Clamp01((desired.x - origin.x) / Mathf.Max(0.001f, size.x));
                float normalizedZ = Mathf.Clamp01((desired.z - origin.z) / Mathf.Max(0.001f, size.z));
                float slope = terrain.terrainData.GetSteepness(normalizedX, normalizedZ);
                if (slope > maxSlope) continue;
                float height = terrain.SampleHeight(desired) + origin.y;
                grounded = new Vector3(desired.x, height, desired.z);
                if (HasWaterAbove(grounded)) continue;
                return true;
            }

            // Interior maps may be made entirely from FBX meshes and contain no Terrain.
            // Select the lowest walkable static surface; never select another live entity.
            if (terrains.Count == 0 && TrySampleStaticGeometry(desired, maxSlope, out grounded)) return true;
            grounded = default;
            return false;
        }

        static bool TrySampleStaticGeometry(Vector3 desired, float maxSlope, out Vector3 grounded)
        {
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(desired.x, 5000f, desired.z), Vector3.down, 10000f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.point.y.CompareTo(right.point.y));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || IsWater(hit.collider.transform) || IsRuntimeEntity(hit.collider.transform)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > maxSlope) continue;
                grounded = hit.point;
                return true;
            }
            grounded = default;
            return false;
        }

        static bool HasWaterAbove(Vector3 terrainPoint)
        {
            Ray ray = new Ray(terrainPoint + Vector3.up * 2000f, Vector3.down);
            foreach (Collider collider in waterColliders)
            {
                if (collider == null || !collider.enabled) continue;
                if (collider.Raycast(ray, out RaycastHit hit, 4000f) && hit.point.y > terrainPoint.y + WaterClearance)
                    return true;
            }
            return false;
        }

        static void EnsureCache()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (cachedSceneHandle == scene.handle) return;
            cachedSceneHandle = scene.handle;
            terrains.Clear();
            waterColliders.Clear();

            foreach (Terrain terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                if (terrain != null && terrain.gameObject.scene == scene && terrain.terrainData != null) terrains.Add(terrain);

            foreach (MeshFilter filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (filter == null || filter.gameObject.scene != scene || filter.sharedMesh == null || !IsWater(filter.transform)) continue;
                MeshCollider collider = filter.GetComponent<MeshCollider>();
                if (collider == null)
                {
                    collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    collider.hideFlags = HideFlags.DontSave;
                }
                waterColliders.Add(collider);
            }
        }

        static bool ContainsXZ(Terrain terrain, float x, float z)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            const float epsilon = 0.02f;
            return x >= origin.x - epsilon && x <= origin.x + size.x + epsilon &&
                   z >= origin.z - epsilon && z <= origin.z + size.z + epsilon;
        }

        static bool IsWater(Transform current)
        {
            while (current != null)
            {
                if (current.name.Equals("Water", StringComparison.OrdinalIgnoreCase) ||
                    current.name.StartsWith("Water_", StringComparison.OrdinalIgnoreCase)) return true;
                current = current.parent;
            }
            return false;
        }

        static bool IsRuntimeEntity(Transform current)
        {
            while (current != null)
            {
                if (current.name.Equals("Metin3 Panel Live Placements", StringComparison.OrdinalIgnoreCase) ||
                    current.GetComponent<Metin2MobCombatant>() != null) return true;
                current = current.parent;
            }
            return false;
        }

        static float Deterministic01(int seed)
        {
            uint value = (uint)seed * 747796405u + 2891336453u;
            value = ((value >> ((int)(value >> 28) + 4)) ^ value) * 277803737u;
            value = (value >> 22) ^ value;
            return (value & 0x00ffffff) / 16777215f;
        }
    }
}
