using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metin3Dev.Panel
{
    [DefaultExecutionOrder(-800)]
    public sealed class Metin3LiveWorldSpawner : MonoBehaviour
    {
        const string CatalogResource = "Metin3EntityPrefabCatalog";
        readonly Dictionary<int, Metin3EntityData> entities = new Dictionary<int, Metin3EntityData>();
        readonly Dictionary<int, Metin3GroupData> groups = new Dictionary<int, Metin3GroupData>();
        Metin3EntityPrefabCatalog catalog;
        Transform runtimeRoot;
        string appliedVersion;

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
            int spawned = 0;
            foreach (Metin3WorldPlacementData placement in payload.worldPlacements ?? Array.Empty<Metin3WorldPlacementData>())
            {
                if (placement == null || !SceneMatches(sceneName, placement.map_code)) continue;
                spawned += SpawnPlacement(placement);
            }
            Debug.Log($"[Metin3 Panel] {sceneName} sahnesine {spawned} canlı varlık uygulandı.");
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
                    for (int index = 0; index < members.Length; index++)
                        if (SpawnEntity(members[index], placement, index + repetition * members.Length, members.Length * repeat)) spawned++;
            }
            else
            {
                for (int index = 0; index < repeat; index++)
                    if (SpawnEntity(placement.target_vnum, placement, index, repeat)) spawned++;
            }
            return spawned;
        }

        bool SpawnEntity(int vnum, Metin3WorldPlacementData placement, int index, int total)
        {
            if (!entities.TryGetValue(vnum, out Metin3EntityData entity)) return false;
            float radius = placement.radius > 0f ? placement.radius : total > 1 ? 2.5f : 0f;
            float angle = total > 1 ? index * Mathf.PI * 2f / total : 0f;
            Vector3 position = new Vector3(placement.x + Mathf.Cos(angle) * radius, placement.z, placement.y + Mathf.Sin(angle) * radius);
            if (Mathf.Abs(placement.z) < 0.001f && Physics.Raycast(new Vector3(position.x, 5000f, position.z), Vector3.down, out RaycastHit hit, 10000f))
                position.y = hit.point.y;
            GameObject prefab = catalog != null ? catalog.Resolve(entity.folder) : null;
            if (prefab == null)
            {
                Debug.LogError($"[Metin3 Panel] Gerçek model bulunamadı. VNUM={entity.vnum}, klasör='{entity.folder}'. Geçici şekil üretilmedi.");
                return false;
            }
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, placement.direction, 0f), runtimeRoot);
            float originalScale = entity.size > 0 ? entity.size / 100f : 1f;
            instance.transform.localScale *= originalScale;
            instance.name = $"Panel_{placement.id}_{entity.vnum}_{entity.name}";
            Metin3ManagedEntity managed = instance.GetComponent<Metin3ManagedEntity>() ?? instance.AddComponent<Metin3ManagedEntity>();
            managed.Configure(entity, placement.id, placement.respawn_seconds);
            Metin2Dev.Gameplay.Metin2MobCombatant combatant = instance.GetComponent<Metin2Dev.Gameplay.Metin2MobCombatant>() ??
                instance.AddComponent<Metin2Dev.Gameplay.Metin2MobCombatant>();
            combatant.Configure(entity, placement.respawn_seconds);
            return true;
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
