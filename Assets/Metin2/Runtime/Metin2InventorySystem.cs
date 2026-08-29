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
    }

    public static class Metin2InventoryService
    {
        public const int SlotCount = 90;
        static readonly Metin2InventoryEntry[] slots = new Metin2InventoryEntry[SlotCount];
        static bool initialized;
        public static event Action Changed;

        public static Metin2InventoryEntry Get(int slot)
        {
            EnsureInitialized();
            return slot >= 0 && slot < slots.Length ? slots[slot] : null;
        }

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            Add(10, "Kılıç +0", 1, false);
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
            slots[empty] = new Metin2InventoryEntry { vnum = vnum, name = string.IsNullOrWhiteSpace(name) ? "İtem " + vnum : name, count = count, stackable = stackable };
            Changed?.Invoke();
            return true;
        }

        public static bool Use(int slot)
        {
            Metin2InventoryEntry entry = Get(slot);
            if (entry == null || Metin2PlayerState.Local == null) return false;
            bool used;
            switch (entry.vnum)
            {
                case 27001:
                case 27002:
                case 27003:
                    Metin2PlayerState.Local.Heal(Mathf.RoundToInt(Metin2PlayerState.Local.MaxHp * 0.35f));
                    used = true;
                    break;
                case 27004:
                case 27005:
                case 27006:
                    Metin2PlayerState.Local.RestoreSp(Mathf.RoundToInt(Metin2PlayerState.Local.MaxSp * 0.35f));
                    used = true;
                    break;
                default:
                    Metin2ChatService.Append(Metin2ChatChannel.Info, entry.name + " şu anda kullanılamıyor.");
                    used = false;
                    break;
            }
            if (!used) return false;
            entry.count--;
            if (entry.count <= 0) slots[slot] = null;
            Changed?.Invoke();
            return true;
        }

        public static void RollDrops(int entityId, int vnum, int playerLevel)
        {
            Metin3PanelPayload payload = Metin3PanelRuntime.Current;
            if (payload?.drops == null || payload.items == null) return;
            if (entityId <= 0)
            {
                Metin3EntityData entity = payload.entities?.FirstOrDefault(item => item != null && item.vnum == vnum);
                entityId = entity != null ? entity.id : 0;
            }
            foreach (Metin3DropData drop in payload.drops)
            {
                if (drop == null || drop.entity_id != entityId || playerLevel < drop.min_level || playerLevel > drop.max_level) continue;
                float chance = Mathf.Clamp(drop.chance * Metin3PanelRuntime.DropRate, 0f, 100f);
                if (UnityEngine.Random.Range(0f, 100f) > chance) continue;
                Metin3ItemData item = payload.items.FirstOrDefault(candidate => candidate != null && candidate.id == drop.item_id);
                if (item == null) continue;
                int amount = UnityEngine.Random.Range(Mathf.Max(1, drop.min_count), Mathf.Max(drop.min_count, drop.max_count) + 1);
                if (Add(item.vnum, item.name, amount, item.stackable != 0))
                    Metin2ChatService.Append(Metin2ChatChannel.Info, $"{item.name} x{amount} elde ettin.");
            }
        }
    }
}
