using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Metin3Dev.Panel;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2MobCombatant : MonoBehaviour, IMetin2Damageable
    {
        public static event System.Action<Metin2MobCombatant> SelectedChanged;
        static readonly HashSet<Metin2MobCombatant> Active = new HashSet<Metin2MobCombatant>();

        [SerializeField] int vnum;
        [SerializeField] string displayName = "Mob";
        [SerializeField] string entityType = "mob";
        [SerializeField] int level = 1;
        [SerializeField] int maxHp = 100;
        [SerializeField] int currentHp = 100;
        [SerializeField] int minDamage = 5;
        [SerializeField] int maxDamage = 9;
        [SerializeField] int defense = 2;
        [SerializeField] int experience = 32;
        [SerializeField] float sightRange = 8f;
        [SerializeField] float attackRange = 2.1f;
        [SerializeField] float moveSpeed = 1.8f;
        [SerializeField] float attackInterval = 1.5f;
        [SerializeField] int respawnSeconds = 30;
        [SerializeField] int entityId;

        Component legacy;
        Component animationRuntime;
        Metin2PlayerState target;
        Collider[] colliders;
        Renderer[] renderers;
        Vector3 home;
        float groundOffset;
        float nextAttackAt;
        bool dead;
        bool configured;
        bool groundReady;
        Vector2 lastGroundedXZ;
        float lastGroundedRootY;

        public int Vnum => vnum;
        public string DisplayName => displayName;
        public int Level => level;
        public int CurrentHp => legacy != null ? ReadInt(legacy, "CurrentHp", currentHp) : currentHp;
        public int MaxHp => legacy != null ? ReadInt(legacy, "MaxHp", maxHp) : Mathf.Max(1, maxHp);
        public float HpRatio => Mathf.Clamp01(CurrentHp / (float)MaxHp);
        public bool IsDead => legacy != null ? ReadBool(legacy, "IsDead", dead) : dead;
        public bool UsesLegacyTargetUi => legacy != null;
        public bool IsAttackable => !IsDead && !string.Equals(entityType, "npc", System.StringComparison.OrdinalIgnoreCase);

        void Awake()
        {
            animationRuntime = GetComponent("MobAnimationRuntime");
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        void Start()
        {
            if (!configured) ConfigureFromComponents();
            EnsureCollider();
            CaptureGroundOffset();
            home = transform.position;
        }

        void LateUpdate()
        {
            if (!groundReady || IsDead) return;
            Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
            if ((currentXZ - lastGroundedXZ).sqrMagnitude < 0.000001f && Mathf.Abs(transform.position.y - lastGroundedRootY) < 0.001f) return;
            SnapToGround();
        }

        public static Metin2MobCombatant FindNearestAttackable(Vector3 origin, float maximumDistance)
        {
            Metin2MobCombatant nearest = null;
            float best = maximumDistance * maximumDistance;
            Active.RemoveWhere(item => item == null);
            foreach (Metin2MobCombatant item in Active)
            {
                if (item == null || !item.isActiveAndEnabled || !item.IsAttackable) continue;
                Vector3 delta = item.transform.position - origin;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = item;
            }
            return nearest;
        }

        public void Configure(Metin3EntityData data, int respawn)
        {
            if (data == null) return;
            vnum = data.vnum;
            entityId = data.id;
            displayName = string.IsNullOrWhiteSpace(data.name) ? "Mob" : data.name;
            entityType = string.IsNullOrWhiteSpace(data.type) ? "mob" : data.type;
            level = Mathf.Max(1, data.level);
            maxHp = Mathf.Max(1, Mathf.RoundToInt(data.hp * Metin3PanelRuntime.MobHpRate));
            currentHp = maxHp;
            minDamage = Mathf.Max(1, Mathf.RoundToInt(data.min_damage * Metin3PanelRuntime.MobDamageRate));
            maxDamage = Mathf.Max(minDamage, Mathf.RoundToInt(data.max_damage * Metin3PanelRuntime.MobDamageRate));
            defense = Mathf.Max(0, data.defense);
            experience = Mathf.Max(0, Mathf.RoundToInt(data.exp * Metin3PanelRuntime.ExperienceRate));
            attackInterval = Mathf.Clamp(2f - data.attack_speed / 150f, 0.55f, 2.2f);
            moveSpeed = Mathf.Clamp(data.move_speed / 55f, 0.8f, 4f);
            respawnSeconds = Mathf.Max(1, respawn);
            configured = true;
        }

        public void Configure(Component data)
        {
            if (data == null) return;
            legacy = data;
            vnum = ReadInt(data, "mobId", 0);
            displayName = ReadString(data, "mobName", "Mob");
            entityType = ReadString(data, "type", "mob");
            level = Mathf.Max(1, ReadInt(data, "level", 1));
            maxHp = Mathf.Max(1, ReadInt(data, "maxHp", 100));
            currentHp = ReadInt(data, "CurrentHp", maxHp);
            minDamage = Mathf.Max(1, ReadInt(data, "minDamage", 5));
            maxDamage = Mathf.Max(minDamage, ReadInt(data, "maxDamage", 9));
            defense = Mathf.Max(0, ReadInt(data, "def", 0));
            experience = Mathf.Max(0, Mathf.RoundToInt(ReadFloat(data, "exp", 0f)));
            int aggressiveSight = ReadInt(data, "aggressiveSight", 0);
            int sourceAttackRange = ReadInt(data, "attackRange", 0);
            int attackSpeed = ReadInt(data, "attackSpeed", 100);
            int sourceMoveSpeed = ReadInt(data, "moveSpeed", 100);
            sightRange = aggressiveSight > 0 ? Mathf.Clamp(aggressiveSight * 0.01f, 4f, 25f) : 8f;
            attackRange = sourceAttackRange > 0 ? Mathf.Clamp(sourceAttackRange * 0.01f, 1.5f, 5f) : 2.1f;
            attackInterval = Mathf.Clamp(2f - attackSpeed / 150f, 0.55f, 2.2f);
            moveSpeed = Mathf.Clamp(sourceMoveSpeed / 55f, 0.8f, 4f);
            respawnSeconds = Mathf.Max(1, ReadInt(data, "respawnSeconds", 30));
            configured = true;
        }

        void ConfigureFromComponents()
        {
            legacy = GetComponent("MobRuntimeData");
            if (legacy != null) { Configure(legacy); return; }
            Metin3ManagedEntity panelEntity = GetComponent<Metin3ManagedEntity>();
            if (panelEntity == null) return;
            vnum = panelEntity.vnum;
            displayName = panelEntity.displayName;
            entityType = panelEntity.entityType;
            level = Mathf.Max(1, panelEntity.level);
            maxHp = Mathf.Max(1, panelEntity.maxHp);
            currentHp = maxHp;
            minDamage = Mathf.Max(1, panelEntity.minDamage);
            maxDamage = Mathf.Max(minDamage, panelEntity.maxDamage);
            defense = Mathf.Max(0, panelEntity.defense);
            experience = Mathf.Max(0, Mathf.RoundToInt(panelEntity.experience));
            respawnSeconds = Mathf.Max(1, panelEntity.respawnSeconds);
            configured = true;
        }

        void Update()
        {
            if (!configured || IsDead || !CanMoveAndAttack()) return;
            if (target == null || target.IsDead) target = Metin2PlayerState.Local;
            if (target == null) return;
            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > sightRange || Vector3.Distance(home, transform.position) > sightRange * 2.5f)
            {
                target = null;
                ReturnHome();
                return;
            }
            if (distance > attackRange)
            {
                Move(delta.normalized);
                return;
            }
            if (delta.sqrMagnitude > 0.001f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), 540f * Time.deltaTime);
            if (Time.time < nextAttackAt) return;
            nextAttackAt = Time.time + attackInterval;
            Invoke(animationRuntime, "PlayAttack");
            StartCoroutine(DealDamageAfterDelay(target, 0.35f));
        }

        bool CanMoveAndAttack()
        {
            return string.Equals(entityType, "mob", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entityType, "monster", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entityType, "boss", System.StringComparison.OrdinalIgnoreCase);
        }

        void Move(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
            Vector3 candidate = transform.position + direction * moveSpeed * Time.deltaTime;
            if (!Metin2WorldSurface.TrySample(candidate, 32f, out Vector3 grounded))
            {
                Invoke(animationRuntime, "SetMoveAmount", 0f);
                return;
            }
            grounded.y += groundOffset;
            transform.position = grounded;
            Invoke(animationRuntime, "SetMoveAmount", 1f);
        }

        void CaptureGroundOffset()
        {
            if (!Metin2WorldSurface.TrySample(transform.position, 89f, out Vector3 surface)) return;
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            Renderer first = null;
            foreach (Renderer renderer in renderers)
                if (renderer != null && !(renderer is ParticleSystemRenderer)) { first = renderer; break; }
            if (first != null)
            {
                Bounds bounds = first.bounds;
                foreach (Renderer renderer in renderers)
                    if (renderer != null && renderer != first && !(renderer is ParticleSystemRenderer)) bounds.Encapsulate(renderer.bounds);
                float feetCorrection = surface.y - bounds.min.y;
                if (!float.IsNaN(feetCorrection) && !float.IsInfinity(feetCorrection))
                    transform.position += Vector3.up * feetCorrection;
            }
            // Preserve the model's real root-to-foot pivot distance. Giants and stones
            // can legitimately use a large root offset, so no guessed clamp is used.
            groundOffset = transform.position.y - surface.y;
            groundReady = true;
            SnapToGround();
        }

        void SnapToGround()
        {
            if (!Metin2WorldSurface.TrySample(transform.position, 89f, out Vector3 surface)) return;
            Vector3 position = transform.position;
            position.y = surface.y + groundOffset;
            transform.position = position;
            lastGroundedXZ = new Vector2(position.x, position.z);
            lastGroundedRootY = position.y;
        }

        void ReturnHome()
        {
            Vector3 delta = home - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.1f) Move(delta.normalized);
            else Invoke(animationRuntime, "SetMoveAmount", 0f);
        }

        IEnumerator DealDamageAfterDelay(Metin2PlayerState victim, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (victim == null || victim.IsDead || IsDead) yield break;
            Vector3 delta = victim.transform.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude > attackRange + 0.8f) yield break;
            victim.ReceiveDamage(UnityEngine.Random.Range(minDamage, maxDamage + 1), this);
        }

        public void Select(bool selected)
        {
            Invoke(legacy, "SetSelected", selected);
            if (selected) SelectedChanged?.Invoke(this);
            else SelectedChanged?.Invoke(null);
        }

        public void ReceiveMetin2Hit(Metin2PlayerController attacker, Metin2MotionRecord motion, Metin2MotionEvent motionEvent)
        {
            if (!IsAttackable) return;
            Metin2PlayerState state = attacker != null ? attacker.GetComponent<Metin2PlayerState>() : Metin2PlayerState.Local;
            float multiplier = motion != null && string.Equals(motion.mode, "skill", System.StringComparison.OrdinalIgnoreCase) ? 1.8f : 1f;
            int rawDamage = state != null ? state.RollAttackDamage(multiplier) : 10;
            TakeDamage(rawDamage, state);
        }

        public void TakeDamage(int rawDamage, Metin2PlayerState attacker)
        {
            if (IsDead) return;
            if (legacy != null)
            {
                Invoke(legacy, "TakeDamage", rawDamage);
                if (ReadBool(legacy, "IsDead", false)) Die(attacker);
                SelectedChanged?.Invoke(this);
                return;
            }
            int finalDamage = Mathf.Max(1, rawDamage - defense);
            currentHp = Mathf.Max(0, currentHp - finalDamage);
            if (currentHp > 0) Invoke(animationRuntime, "PlayHit");
            SelectedChanged?.Invoke(this);
            if (currentHp <= 0) Die(attacker);
        }

        void Die(Metin2PlayerState killer)
        {
            if (dead) return;
            dead = true;
            target = null;
            if (legacy == null || !ReadBool(legacy, "IsDead", false)) Invoke(animationRuntime, "PlayDead");
            SetColliders(false);
            killer ??= Metin2PlayerState.Local;
            if (killer != null)
            {
                killer.GainExperience(experience);
                killer.AddGold(Mathf.Max(1, Mathf.RoundToInt(level * UnityEngine.Random.Range(2, 6) * Metin3PanelRuntime.YangRate)));
                Metin2InventoryService.RollDrops(entityId, vnum, killer.Level, transform.position);
            }
            Metin2QuestService.ReportKill(vnum);
            Metin2ChatService.Append(Metin2ChatChannel.Info, $"{displayName} yenildi. +{experience} EXP");
            SelectedChanged?.Invoke(null);
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(1, respawnSeconds));
            transform.position = home;
            SnapToGround();
            dead = false;
            currentHp = maxHp;
            if (legacy != null) Invoke(legacy, "Revive");
            else Invoke(animationRuntime, "Revive");
            SetRenderers(true);
            SetColliders(true);
        }

        void SetColliders(bool value)
        {
            if (colliders == null || colliders.Length == 0) colliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider item in colliders) if (item != null) item.enabled = value;
        }

        void SetRenderers(bool value)
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer item in renderers) if (item != null) item.enabled = value;
        }

        void EnsureCollider()
        {
            if (GetComponentInChildren<Collider>(true) != null) return;
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.center = transform.InverseTransformPoint(bounds.center);
            Vector3 scale = transform.lossyScale;
            box.size = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)), bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)), bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            colliders = GetComponentsInChildren<Collider>(true);
        }

        static object ReadMember(Component component, string name)
        {
            if (component == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = component.GetType().GetProperty(name, flags);
            if (property != null) return property.GetValue(component);
            FieldInfo field = component.GetType().GetField(name, flags);
            return field != null ? field.GetValue(component) : null;
        }

        static int ReadInt(Component component, string name, int fallback)
        {
            object value = ReadMember(component, name);
            try { return value != null ? System.Convert.ToInt32(value) : fallback; }
            catch { return fallback; }
        }

        static float ReadFloat(Component component, string name, float fallback)
        {
            object value = ReadMember(component, name);
            try { return value != null ? System.Convert.ToSingle(value) : fallback; }
            catch { return fallback; }
        }

        static bool ReadBool(Component component, string name, bool fallback)
        {
            object value = ReadMember(component, name);
            try { return value != null ? System.Convert.ToBoolean(value) : fallback; }
            catch { return fallback; }
        }

        static string ReadString(Component component, string name, string fallback)
        {
            string value = ReadMember(component, name) as string;
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        static void Invoke(Component component, string method, params object[] arguments)
        {
            if (component == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo target = component.GetType().GetMethod(method, flags);
            if (target != null) target.Invoke(component, arguments);
        }
    }
}
