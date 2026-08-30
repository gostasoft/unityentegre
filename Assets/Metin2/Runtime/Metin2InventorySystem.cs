using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Metin3Dev.Panel;

namespace Metin2Dev.Gameplay
{
    [Serializable]
    public sealed class Metin2InventoryEntry
    {
        public int vnum;
        public string name;
        public int count;
        public bool stackable;

        public Metin2InventoryEntry Clone(int amount = -1) => new Metin2InventoryEntry
        {
            vnum = vnum, name = name, count = amount < 0 ? count : amount, stackable = stackable
        };
    }

    public static class Metin2InventoryService
    {
        public const int SlotCount = 90;
        static readonly Metin2InventoryEntry[] slots = new Metin2InventoryEntry[SlotCount];
        static readonly Dictionary<Metin2EquipmentSlot, Metin2InventoryEntry> equipped = new Dictionary<Metin2EquipmentSlot, Metin2InventoryEntry>();
        static bool initialized;
        public static event Action Changed;
        public static event Action EquippedChanged;

        public static int AttackMinBonus => EquipmentValue(Metin2EquipmentSlot.Weapon, 3);
        public static int AttackMaxBonus => EquipmentValue(Metin2EquipmentSlot.Weapon, 4);
        public static int DefenseBonus => EquipmentValue(Metin2EquipmentSlot.Body, 1) + EquipmentValue(Metin2EquipmentSlot.Head, 1) + EquipmentValue(Metin2EquipmentSlot.Shield, 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Array.Clear(slots, 0, slots.Length);
            equipped.Clear();
            initialized = false;
            Changed = null;
            EquippedChanged = null;
        }

        public static Metin2InventoryEntry Get(int slot)
        {
            EnsureInitialized();
            return slot >= 0 && slot < slots.Length ? slots[slot] : null;
        }

        public static Metin2InventoryEntry GetEquipped(Metin2EquipmentSlot slot)
        {
            EnsureInitialized();
            equipped.TryGetValue(slot, out Metin2InventoryEntry entry);
            return entry;
        }

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            Add(10, "Kılıç +0", 1, false);
            Add(11200, "Keşiş Plakası +0", 1, false);
            Add(27001, "Kırmızı İksir (K)", 20, true);
            Add(27004, "Mavi İksir (K)", 20, true);
        }

        public static bool Add(int vnum, string name, int count, bool stackable = true)
        {
            EnsureInitialized();
            count = Mathf.Max(1, count);
            if (stackable)
            {
                Metin2InventoryEntry existing = slots.FirstOrDefault(item => item != null && item.vnum == vnum);
                if (existing != null)
                {
                    existing.count += count;
                    Changed?.Invoke();
                    return true;
                }
            }
            int empty = Array.FindIndex(slots, item => item == null);
            if (empty < 0)
            {
                Metin2ChatService.Append(Metin2ChatChannel.Info, "Envanter dolu.");
                return false;
            }
            slots[empty] = new Metin2InventoryEntry
            {
                vnum = vnum,
                name = string.IsNullOrWhiteSpace(name) ? "Eşya " + vnum : name,
                count = count,
                stackable = stackable
            };
            Changed?.Invoke();
            return true;
        }

        // Original client behavior: double click uses a consumable or equips wearable gear.
        public static bool Use(int slot)
        {
            Metin2InventoryEntry entry = Get(slot);
            if (entry == null) return false;
            Metin2ItemDefinition definition = Metin2ItemDatabase.Get(entry.vnum);
            Metin2EquipmentSlot equipmentSlot = definition?.EquipmentSlot ?? Metin2EquipmentSlot.None;
            if (equipmentSlot != Metin2EquipmentSlot.None) return Equip(slot, equipmentSlot);
            if (Metin2PlayerState.Local == null) return false;
            if (!UseConsumable(entry, definition))
            {
                Metin2ChatService.Append(Metin2ChatChannel.Info, entry.name + " bu hedef olmadan kullanılamaz.");
                return false;
            }
            entry.count--;
            if (entry.count <= 0) slots[slot] = null;
            Changed?.Invoke();
            return true;
        }

        public static bool Equip(int inventorySlot, Metin2EquipmentSlot target)
        {
            Metin2InventoryEntry entry = Get(inventorySlot);
            if (entry == null || target == Metin2EquipmentSlot.None) return false;
            Metin2ItemDefinition definition = Metin2ItemDatabase.Get(entry.vnum);
            if (definition == null || definition.EquipmentSlot != target)
            {
                Metin2ChatService.Append(Metin2ChatChannel.Info, entry.name + " bu ekipman yuvasına takılamaz.");
                return false;
            }
            equipped.TryGetValue(target, out Metin2InventoryEntry previous);
            equipped[target] = entry.Clone(1);
            if (entry.count > 1) entry.count--; else slots[inventorySlot] = null;
            if (previous != null && !Add(previous.vnum, previous.name, previous.count, previous.stackable))
            {
                equipped[target] = previous;
                Add(entry.vnum, entry.name, 1, entry.stackable);
                return false;
            }
            Metin2ChatService.Append(Metin2ChatChannel.Info, entry.name + " kuşanıldı.");
            Changed?.Invoke();
            EquippedChanged?.Invoke();
            Metin2PlayerState.Local?.NotifyEquipmentChanged();
            return true;
        }

        public static bool Unequip(Metin2EquipmentSlot target)
        {
            Metin2InventoryEntry entry = GetEquipped(target);
            if (entry == null || !Add(entry.vnum, entry.name, entry.count, entry.stackable)) return false;
            equipped.Remove(target);
            Metin2ChatService.Append(Metin2ChatChannel.Info, entry.name + " çıkarıldı.");
            Changed?.Invoke();
            EquippedChanged?.Invoke();
            Metin2PlayerState.Local?.NotifyEquipmentChanged();
            return true;
        }

        public static void RollDrops(int entityId, int vnum, int playerLevel, Vector3 worldPosition)
        {
            Metin3PanelPayload payload = Metin3PanelRuntime.Current;
            if (payload?.drops == null || payload.items == null) return;
            if (entityId <= 0)
            {
                Metin3EntityData entity = payload.entities?.FirstOrDefault(item => item != null && item.vnum == vnum);
                entityId = entity != null ? entity.id : 0;
            }
            int dropIndex = 0;
            foreach (Metin3DropData drop in payload.drops)
            {
                if (drop == null || drop.entity_id != entityId || playerLevel < drop.min_level || playerLevel > drop.max_level) continue;
                float chance = Mathf.Clamp(drop.chance * Metin3PanelRuntime.DropRate, 0f, 100f);
                if (UnityEngine.Random.Range(0f, 100f) > chance) continue;
                Metin3ItemData item = payload.items.FirstOrDefault(candidate => candidate != null && candidate.id == drop.item_id);
                if (item == null) continue;
                int amount = UnityEngine.Random.Range(Mathf.Max(1, drop.min_count), Mathf.Max(drop.min_count, drop.max_count) + 1);
                Vector2 circle = UnityEngine.Random.insideUnitCircle * (0.45f + dropIndex * 0.12f);
                Metin2GroundItem.Spawn(new Metin2InventoryEntry
                {
                    vnum = item.vnum, name = item.name, count = amount, stackable = item.stackable != 0
                }, worldPosition + new Vector3(circle.x, 0.25f, circle.y));
                dropIndex++;
            }
        }

        static bool UseConsumable(Metin2InventoryEntry entry, Metin2ItemDefinition definition)
        {
            if (entry.vnum >= 27001 && entry.vnum <= 27003)
            {
                int amount = definition != null && definition.values[0] > 0 ? definition.values[0] : Mathf.RoundToInt(Metin2PlayerState.Local.MaxHp * 0.35f);
                Metin2PlayerState.Local.Heal(amount);
                return true;
            }
            if (entry.vnum >= 27004 && entry.vnum <= 27006)
            {
                int amount = definition != null && definition.values[0] > 0 ? definition.values[0] : Mathf.RoundToInt(Metin2PlayerState.Local.MaxSp * 0.35f);
                Metin2PlayerState.Local.RestoreSp(amount);
                return true;
            }
            if (definition == null || !definition.IsUseItem) return false;
            if (definition.subType.IndexOf("POTION", StringComparison.OrdinalIgnoreCase) < 0 && definition.subType.IndexOf("ABILITY_UP", StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (definition.values[0] > 0) Metin2PlayerState.Local.Heal(definition.values[0]);
            if (definition.values[1] > 0) Metin2PlayerState.Local.RestoreSp(definition.values[1]);
            return definition.values[0] > 0 || definition.values[1] > 0;
        }

        static int EquipmentValue(Metin2EquipmentSlot slot, int valueIndex)
        {
            Metin2InventoryEntry entry = GetEquipped(slot);
            Metin2ItemDefinition definition = entry != null ? Metin2ItemDatabase.Get(entry.vnum) : null;
            return definition != null && valueIndex >= 0 && valueIndex < definition.values.Length ? Mathf.Max(0, definition.values[valueIndex]) : 0;
        }
    }
}
