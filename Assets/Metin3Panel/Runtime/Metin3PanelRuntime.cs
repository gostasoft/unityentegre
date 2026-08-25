using System;

namespace Metin3Dev.Panel
{
    public static class Metin3PanelRuntime
    {
        public static event Action<Metin3PanelPayload> Updated;
        public static Metin3PanelPayload Current { get; private set; }
        public static bool IsReady => Current != null;
        public static float ExperienceRate => Current?.settings?.Float(Current.settings.exp_rate, 1f) ?? 1f;
        public static float DropRate => Current?.settings?.Float(Current.settings.drop_rate, 1f) ?? 1f;
        public static float YangRate => Current?.settings?.Float(Current.settings.yang_rate, 1f) ?? 1f;
        public static float MobHpRate => Current?.settings?.Float(Current.settings.mob_hp_rate, 1f) ?? 1f;
        public static float MobDamageRate => Current?.settings?.Float(Current.settings.mob_damage_rate, 1f) ?? 1f;
        public static bool Maintenance => string.Equals(Current?.settings?.server_maintenance, "true", StringComparison.OrdinalIgnoreCase);

        internal static void Apply(Metin3PanelPayload payload)
        {
            if (payload == null) return;
            Current = payload;
            Updated?.Invoke(payload);
        }
    }

    [Serializable] public sealed class Metin3PanelPayload
    {
        public string version;
        public Metin3GlobalSettings settings;
        public Metin3MapData[] maps;
        public Metin3EntityData[] entities;
        public Metin3EntityData[] runtimeEntities;
        public Metin3ItemData[] items;
        public Metin3SpawnData[] spawns;
        public Metin3WorldPlacementData[] worldPlacements;
        public Metin3GroupData[] groups;
        public Metin3DropData[] drops;
        public Metin3ShopData[] shops;
        public Metin3ShopItemData[] shopItems;
        public Metin3EventData[] events;
        public Metin3SanctionData[] sanctions;
    }

    [Serializable] public sealed class Metin3GlobalSettings
    {
        public string exp_rate = "1";
        public string drop_rate = "1";
        public string yang_rate = "1";
        public string mob_hp_rate = "1";
        public string mob_damage_rate = "1";
        public string server_maintenance = "false";
        public float Float(string value, float fallback) => float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }
    [Serializable] public sealed class Metin3MapData { public int id; public string code; public string name; public int width; public int height; }
    [Serializable] public sealed class Metin3EntityData { public int id; public int vnum; public string name; public string type; public string rank; public int level; public int hp; public float exp; public int min_damage; public int max_damage; public int defense; public int attack_speed; public int move_speed; public string folder; }
    [Serializable] public sealed class Metin3ItemData { public int id; public int vnum; public string name; public string category; public long buy_price; public long sell_price; public int stackable; }
    [Serializable] public sealed class Metin3SpawnData { public int id; public int map_id; public int entity_id; public float x; public float y; public float z; public float direction; public int respawn_seconds; public int group_size; }
    [Serializable] public sealed class Metin3WorldPlacementData { public int id; public int map_id; public string map_code; public string target_kind; public int target_vnum; public float x; public float y; public float z; public float direction; public float radius; public int respawn_seconds; public int count; }
    [Serializable] public sealed class Metin3GroupData { public int vnum; public string name; public int leaderVnum; public int[] members; }
    [Serializable] public sealed class Metin3DropData { public int id; public int entity_id; public int item_id; public float chance; public int min_count; public int max_count; public int min_level; public int max_level; }
    [Serializable] public sealed class Metin3ShopData { public int id; public int entity_id; public string name; }
    [Serializable] public sealed class Metin3ShopItemData { public int id; public int shop_id; public int item_id; public long buy_price; public long sell_price; public int position; }
    [Serializable] public sealed class Metin3EventData { public int id; public string name; public string description; public string target_type; public string start_at; public string end_at; public float multiplier; }
    [Serializable] public sealed class Metin3SanctionData { public string account; public string character_name; public string ban_until; public string ban_reason; public string mute_until; }
}
