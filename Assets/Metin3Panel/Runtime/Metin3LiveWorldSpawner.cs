using System;
using System.Collections.Generic;
using System.Linq;
using Metin2Dev.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metin3Dev.Panel
{
    [DefaultExecutionOrder(-800)]
    public sealed class Metin3LiveWorldSpawner : MonoBehaviour
    {
        const string CatalogResource = "Metin3EntityPrefabCatalog";
        // Player GR2 models use this same conversion in Metin2GameplayBootstrap.
        // Keeping entities on the identical source-unit conversion preserves the
        // relative size authored in mob_proto instead of applying a visual guess.
        const float MetinModelToUnityScale = 2f;
        readonly Dictionary<int, Metin3EntityData> entities = new Dictionary<int, Metin3EntityData>();
        readonly Dictionary<int, Metin3GroupData> groups = new Dictionary<int, Metin3GroupData>();
        readonly HashSet<string> reportedMissingModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Metin3EntityPrefabCatalog catalog;
        Transform runtimeRoot;
        string appliedVersion;
        bool retryWhenCatalogReady;
        float nextCatalogRetry;
        bool coordinateRevisionApplied;
        Metin3MapData activeMap;
        Bounds activeTerrainBounds;
        bool hasTerrainBounds;
        int unsafeSurfaceCount;
        int missingModelCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<Metin3LiveWorldSpawner>() != null) return;
            GameObject root = new GameObject("Metin3 Live World Spawner");
            DontDestroyOnLoad(root);
            root.AddComponent<Metin3LiveWorldSpawner>();
        }

        void Awake()
        {
            catalog = Resources.Load<Metin3EntityPrefabCatalog>(CatalogResource);
            Metin3PanelRuntime.Updated += OnPanelUpdated;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (Metin3PanelRuntime.Current != null) OnPanelUpdated(Metin3PanelRuntime.Current);
        }

        void OnDestroy()
        {
            Metin3PanelRuntime.Updated -= OnPanelUpdated;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Update()
        {
            if (!coordinateRevisionApplied)
            {
                coordinateRevisionApplied = true;
                appliedVersion = null;
                if (Metin3PanelRuntime.Current != null) Apply(Metin3PanelRuntime.Current);
                return;
            }
            if (!retryWhenCatalogReady || Time.unscaledTime < nextCatalogRetry) return;
            nextCatalogRetry = Time.unscaledTime + 1f;
            if (Resources.Load<GameObject>("EntityPrefabs/stray_dog") == null) return;
            retryWhenCatalogReady = false;
            appliedVersion = null;
            if (Metin3PanelRuntime.Current != null) Apply(Metin3PanelRuntime.Current);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            appliedVersion = null;
            if (Metin3PanelRuntime.Current != null) Apply(Metin3PanelRuntime.Current);
        }

        void OnPanelUpdated(Metin3PanelPayload payload)
        {
            if (payload == null || payload.version == appliedVersion) return;
            Apply(payload);
        }

        void Apply(Metin3PanelPayload payload)
        {
            appliedVersion = payload.version;
            entities.Clear();
            groups.Clear();
            foreach (Metin3EntityData entity in payload.runtimeEntities ?? payload.entities ?? Array.Empty<Metin3EntityData>())
                if (entity != null) entities[entity.vnum] = entity;
            foreach (Metin3GroupData group in payload.groups ?? Array.Empty<Metin3GroupData>())
                if (group != null) groups[group.vnum] = group;

            if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
            runtimeRoot = new GameObject("Metin3 Panel Live Placements").transform;
            string sceneName = SceneManager.GetActiveScene().name;
            runtimeRoot.SetParent(null);
            activeMap = (payload.maps ?? Array.Empty<Metin3MapData>()).FirstOrDefault(map => map != null && SceneMatches(sceneName, map.code));
            Metin2WorldSurface.Refresh();
            hasTerrainBounds = Metin2WorldSurface.TryGetPlacementBounds(out activeTerrainBounds);
            unsafeSurfaceCount = 0;
            missingModelCount = 0;
            reportedMissingModels.Clear();
            int spawned = 0;
            int matchingPlacements = 0;
            foreach (Metin3WorldPlacementData placement in payload.worldPlacements ?? Array.Empty<Metin3WorldPlacementData>())
            {
                if (placement == null || !SceneMatches(sceneName, placement.map_code)) continue;
                matchingPlacements++;
                spawned += SpawnPlacement(placement);
            }
            retryWhenCatalogReady = matchingPlacements > 0 && spawned == 0 && missingModelCount > 0 && unsafeSurfaceCount == 0;
            nextCatalogRetry = Time.unscaledTime + 1f;
            Debug.Log($"[Metin3 Panel] {sceneName} sahnesine {spawned} canlı varlık uygulandı. " +
                      $"Güvensiz/eğimli/sulu zemin: {unsafeSurfaceCount}, eksik model: {missingModelCount}.");
        }

        int SpawnPlacement(Metin3WorldPlacementData placement)
        {
            int spawned = 0;
            int repeat = Mathf.Max(1, placement.count);
            if (string.Equals(placement.target_kind, "group", StringComparison.OrdinalIgnoreCase))
            {
                if (!groups.TryGetValue(placement.target_vnum, out Metin3GroupData group)) return 0;
                int[] members = group.members ?? Array.Empty<int>();
                for (int repetition = 0; repetition < repeat; repetition++)
                {
                    Vector2 center = PlacementOffset(placement, repetition, repeat);
                    for (int index = 0; index < members.Length; index++)
                    {
                        float angle = members.Length > 1 ? index * Mathf.PI * 2f / members.Length : 0f;
                        Vector2 formation = members.Length > 1 ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.5f : Vector2.zero;
                        if (SpawnEntity(members[index], placement, center + formation, index + repetition * members.Length)) spawned++;
                    }
                }
            }
            else
            {
                for (int index = 0; index < repeat; index++)
                    if (SpawnEntity(placement.target_vnum, placement, PlacementOffset(placement, index, repeat), index)) spawned++;
            }
            return spawned;
        }

        Vector2 PlacementOffset(Metin3WorldPlacementData placement, int index, int total)
        {
            if (placement.spread_x > 0f || placement.spread_y > 0f)
                return new Vector2((Deterministic01(placement.id, index, 17) * 2f - 1f) * placement.spread_x,
                    (Deterministic01(placement.id, index, 53) * 2f - 1f) * placement.spread_y);
            float radius = placement.radius > 0f ? placement.radius : total > 1 ? 2.5f : 0f;
            float angle = total > 1 ? index * Mathf.PI * 2f / total : 0f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        static float Deterministic01(int placementId, int index, int salt)
        {
            uint value = (uint)(placementId * 73856093 ^ index * 19349663 ^ salt * 83492791);
            value ^= value >> 13; value *= 1274126177u; value ^= value >> 16;
            return (value & 0x00ffffff) / 16777215f;
        }

        bool SpawnEntity(int vnum, Metin3WorldPlacementData placement, Vector2 offset, int instanceIndex)
        {
            if (!entities.TryGetValue(vnum, out Metin3EntityData entity)) return false;
            if (placement.spawn_percent > 0f && placement.spawn_percent < 100f && Deterministic01(placement.id, instanceIndex, 97) * 100f > placement.spawn_percent) return false;
            // Original regen files use the source map raster coordinate system. The
            // imported Unity terrain is scaled and its root can be translated, so map
            // those values into the actual combined Terrain bounds. Manual panel
            // placements already contain the Unity world X/Z displayed by the HUD.
            bool originalRegen = !string.IsNullOrEmpty(placement.source_key) &&
                placement.source_key.StartsWith("original:", StringComparison.OrdinalIgnoreCase);
            Vector3 desired = OriginalToWorld(placement, offset, originalRegen);
            float maxSlope = MaximumSlope(entity.type);
            float safeSearchRadius = string.Equals(entity.type, "npc", StringComparison.OrdinalIgnoreCase) ? 10f : 24f;
            int surfaceSeed = placement.id * 397 ^ instanceIndex * 7919 ^ entity.vnum;
            if (!Metin2WorldSurface.TryFindSafePosition(desired, maxSlope, safeSearchRadius, surfaceSeed, out Vector3 position))
            {
                unsafeSurfaceCount++;
                return false;
            }
            if (catalog == null) catalog = Resources.Load<Metin3EntityPrefabCatalog>(CatalogResource);
            GameObject prefab = catalog != null ? catalog.Resolve(entity.folder) : null;
            if (prefab == null)
                prefab = Resources.Load<GameObject>("EntityPrefabs/" + Metin3EntityPrefabCatalog.Normalize(entity.folder));
            if (prefab == null)
            {
                missingModelCount++;
                string missingKey = entity.vnum + ":" + entity.folder;
                if (reportedMissingModels.Add(missingKey))
                    Debug.LogError($"[Metin3 Panel] Gerçek model bulunamadı. VNUM={entity.vnum}, klasör='{entity.folder}'. Geçici şekil üretilmedi.");
                return false;
            }
            float direction = placement.direction;
            if (!string.IsNullOrEmpty(placement.source_key) && Mathf.Abs(direction) < 0.001f)
                direction = Deterministic01(placement.id, instanceIndex, 131) * 360f;
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, direction, 0f), runtimeRoot);
            float originalScale = MetinModelToUnityScale * (entity.size > 0 ? entity.size / 100f : 1f);
            instance.transform.localScale *= originalScale;
            AlignRendererFeet(instance, position.y);
            instance.name = $"Panel_{placement.id}_{entity.vnum}_{entity.name}";
            Metin3ManagedEntity managed = instance.GetComponent<Metin3ManagedEntity>() ?? instance.AddComponent<Metin3ManagedEntity>();
            managed.Configure(entity, placement.id, placement.respawn_seconds);
            Metin2Dev.Gameplay.Metin2MobCombatant combatant = instance.GetComponent<Metin2Dev.Gameplay.Metin2MobCombatant>() ??
                instance.AddComponent<Metin2Dev.Gameplay.Metin2MobCombatant>();
            combatant.Configure(entity, placement.respawn_seconds);
            return true;
        }

        Vector3 OriginalToWorld(Metin3WorldPlacementData placement, Vector2 offset, bool originalRegen)
        {
            float sourceX = placement.x + offset.x;
            float sourceY = placement.y + offset.y;
            if (!originalRegen || !hasTerrainBounds)
                return new Vector3(sourceX, placement.z, sourceY);

            float sourceWidth = activeMap != null && activeMap.width > 0 ? activeMap.width : activeTerrainBounds.size.x * 2f;
            float sourceHeight = activeMap != null && activeMap.height > 0 ? activeMap.height : activeTerrainBounds.size.z * 2f;
            float worldX = activeTerrainBounds.min.x + sourceX / Mathf.Max(1f, sourceWidth) * activeTerrainBounds.size.x;
            float worldZ = activeTerrainBounds.min.z + sourceY / Mathf.Max(1f, sourceHeight) * activeTerrainBounds.size.z;
            return new Vector3(worldX, placement.z, worldZ);
        }

        static float MaximumSlope(string entityType)
        {
            if (string.Equals(entityType, "npc", StringComparison.OrdinalIgnoreCase)) return 22f;
            if (string.Equals(entityType, "stone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entityType, "metin", StringComparison.OrdinalIgnoreCase)) return 18f;
            return 30f;
        }

        static void AlignRendererFeet(GameObject instance, float surfaceY)
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null) animator.Update(0f);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && !(renderer is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            float correction = surfaceY - bounds.min.y;
            if (!float.IsNaN(correction) && !float.IsInfinity(correction)) instance.transform.position += Vector3.up * correction;
        }

        static bool SceneMatches(string sceneName, string mapCode)
        {
            if (string.IsNullOrWhiteSpace(mapCode)) return false;
            string scene = Normalize(sceneName);
            string map = Normalize(mapCode);
            return scene == map || scene.Contains(map) || map.Contains(scene);
        }

        static string Normalize(string value) => new string((value ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    public sealed class Metin3ManagedEntity : MonoBehaviour
    {
        public int vnum;
        public string displayName;
        public string entityType;
        public int level;
        public int maxHp;
        public float experience;
        public int minDamage;
        public int maxDamage;
        public int defense;
        public int placementId;
        public int respawnSeconds;

        public void Configure(Metin3EntityData data, int sourcePlacementId, int respawn)
        {
            vnum = data.vnum; displayName = data.name; entityType = data.type; level = data.level;
            maxHp = data.hp; experience = data.exp; minDamage = data.min_damage; maxDamage = data.max_damage;
            defense = data.defense; placementId = sourcePlacementId; respawnSeconds = respawn;
        }
    }
}
