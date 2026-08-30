using System;
using System.Collections;
using UnityEngine;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2PlayerState : MonoBehaviour
    {
        public static Metin2PlayerState Local { get; private set; }
        public static event Action<Metin2PlayerState> LocalChanged;

        [SerializeField] int level;
        [SerializeField] int experience;
        [SerializeField] int nextExperience;
        [SerializeField] int currentHp;
        [SerializeField] int maxHp;
        [SerializeField] int currentSp;
        [SerializeField] int maxSp;
        [SerializeField] int currentStamina;
        [SerializeField] int maxStamina;
        [SerializeField] int vitality;
        [SerializeField] int intelligence;
        [SerializeField] int strength;
        [SerializeField] int dexterity;
        [SerializeField] int gold;
        [SerializeField] bool dead;

        float lastDamageAt;
        float hpRegenAccumulator;
        float spRegenAccumulator;
        Vector3 respawnPoint;

        public event Action Changed;
        public event Action Died;
        public event Action Revived;
        public event Action<int> LevelGained;

        public int Level => level;
        public int Experience => experience;
        public int NextExperience => nextExperience;
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public int CurrentSp => currentSp;
        public int MaxSp => maxSp;
        public int CurrentStamina => currentStamina;
        public int MaxStamina => maxStamina;
        public int Vitality => vitality;
        public int Intelligence => intelligence;
        public int Strength => strength;
        public int Dexterity => dexterity;
        public int Gold => gold;
        public int AttackMin => Mathf.Max(1, level * 2 + strength * 3 + dexterity + Metin2InventoryService.AttackMinBonus);
        public int AttackMax => Mathf.Max(AttackMin + 1, level * 3 + strength * 4 + dexterity * 2 + Metin2InventoryService.AttackMaxBonus);
        public int Defense => Mathf.Max(0, level + vitality * 2 + dexterity / 2 + Metin2InventoryService.DefenseBonus);
        public bool IsDead => dead;

        void Awake()
        {
            Local = this;
            respawnPoint = transform.position;
            InitializeFromSession();
            LocalChanged?.Invoke(this);
        }

        void OnDestroy()
        {
            if (Local != this) return;
            Local = null;
            LocalChanged?.Invoke(null);
        }

        void Update()
        {
            if (dead) return;
            bool changed = false;
            if (Time.time - lastDamageAt >= 4f && currentHp < maxHp)
            {
                hpRegenAccumulator += maxHp * 0.008f * Time.deltaTime;
                int recovered = Mathf.FloorToInt(hpRegenAccumulator);
                if (recovered > 0)
                {
                    hpRegenAccumulator -= recovered;
                    currentHp = Mathf.Min(maxHp, currentHp + recovered);
                    changed = true;
                }
            }
            if (currentSp < maxSp)
            {
                spRegenAccumulator += maxSp * 0.012f * Time.deltaTime;
                int recovered = Mathf.FloorToInt(spRegenAccumulator);
                if (recovered > 0)
                {
                    spRegenAccumulator -= recovered;
                    currentSp = Mathf.Min(maxSp, currentSp + recovered);
                    changed = true;
                }
            }
            if (changed) NotifyChanged();
        }

        void InitializeFromSession()
        {
            level = Mathf.Max(1, Metin2GameplaySession.Level);
            vitality = Mathf.Max(1, Metin2GameplaySession.Vitality);
            intelligence = Mathf.Max(1, Metin2GameplaySession.Intelligence);
            strength = Mathf.Max(1, Metin2GameplaySession.Strength);
            dexterity = Mathf.Max(1, Metin2GameplaySession.Dexterity);
            RecalculateDerivedStats(true);
            experience = 0;
            gold = 0;
            dead = false;
        }

        void RecalculateDerivedStats(bool refill)
        {
            int previousMaxHp = Mathf.Max(1, maxHp);
            int previousMaxSp = Mathf.Max(1, maxSp);
            maxHp = 500 + vitality * 40 + level * 50;
            maxSp = 150 + intelligence * 25 + level * 12;
            maxStamina = 800;
            nextExperience = Mathf.Max(100, level * level * 100);
            currentHp = refill ? maxHp : Mathf.Clamp(Mathf.RoundToInt(currentHp * (maxHp / (float)previousMaxHp)), 1, maxHp);
            currentSp = refill ? maxSp : Mathf.Clamp(Mathf.RoundToInt(currentSp * (maxSp / (float)previousMaxSp)), 0, maxSp);
            currentStamina = refill ? maxStamina : Mathf.Clamp(currentStamina, 0, maxStamina);
        }

        public int RollAttackDamage(float multiplier = 1f)
        {
            return Mathf.Max(1, Mathf.RoundToInt(UnityEngine.Random.Range(AttackMin, AttackMax + 1) * Mathf.Max(0.1f, multiplier)));
        }

        public bool SpendSp(int amount)
        {
            amount = Mathf.Max(0, amount);
            if (currentSp < amount || dead) return false;
            currentSp -= amount;
            NotifyChanged();
            return true;
        }

        public void ReceiveDamage(int rawDamage, Component source = null)
        {
            if (dead) return;
            int damage = Mathf.Max(1, rawDamage - Defense);
            currentHp = Mathf.Max(0, currentHp - damage);
            lastDamageAt = Time.time;
            Metin2ChatService.Append(Metin2ChatChannel.Info, $"{damage} hasar aldın.");
            NotifyChanged();
            if (currentHp > 0) return;
            dead = true;
            Metin2ChatService.Append(Metin2ChatChannel.Info, "Karakterin öldü. Yeniden doğuyorsun...");
            Metin2PlayerController controller = GetComponent<Metin2PlayerController>();
            if (controller != null) controller.enabled = false;
            Died?.Invoke();
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSecondsRealtime(3f);
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.position = respawnPoint;
            if (controller != null) controller.enabled = true;
            currentHp = maxHp;
            currentSp = maxSp;
            currentStamina = maxStamina;
            dead = false;
            Metin2PlayerController movement = GetComponent<Metin2PlayerController>();
            if (movement != null) movement.enabled = true;
            Metin2ChatService.Append(Metin2ChatChannel.Info, "Yeniden doğdun.");
            NotifyChanged();
            Revived?.Invoke();
        }

        public void GainExperience(int amount)
        {
            if (amount <= 0) return;
            experience += amount;
            while (experience >= nextExperience)
            {
                experience -= nextExperience;
                level++;
                vitality++;
                strength++;
                if (level % 2 == 0) dexterity++;
                if (level % 3 == 0) intelligence++;
                RecalculateDerivedStats(true);
                Metin2ChatService.Append(Metin2ChatChannel.Info, $"Seviye atladın! Yeni seviyen: {level}");
                LevelGained?.Invoke(level);
                Metin2QuestService.ReportLevel(level);
            }
            NotifyChanged();
        }

        public void AddGold(int amount)
        {
            gold = Mathf.Max(0, gold + amount);
            NotifyChanged();
        }

        public void Heal(int amount)
        {
            if (dead || amount <= 0) return;
            currentHp = Mathf.Min(maxHp, currentHp + amount);
            NotifyChanged();
        }

        public void RestoreSp(int amount)
        {
            if (dead || amount <= 0) return;
            currentSp = Mathf.Min(maxSp, currentSp + amount);
            NotifyChanged();
        }

        public void NotifyEquipmentChanged()
        {
            NotifyChanged();
        }

        void NotifyChanged()
        {
            Changed?.Invoke();
            LocalChanged?.Invoke(this);
        }
    }
}
