using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Metin2Dev.Frontend;

namespace Metin2Dev.Gameplay
{
    public enum Metin2EquipmentSlot
    {
        Body,
        Head,
        Shoes,
        Wrist,
        Weapon,
        Neck,
        Ear,
        Unique1,
        Unique2,
        Arrow,
        Shield,
        Costume,
        None
    }

    public sealed class Metin2ItemDefinition
    {
        public int vnum;
        public string itemType;
        public string subType;
        public string wearFlags;
        public string iconResource;
        public string modelResource;
        public int size = 1;
        public readonly int[] values = new int[6];

        public bool IsWeapon => string.Equals(itemType, "ITEM_WEAPON", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(itemType, "WEAPON", StringComparison.OrdinalIgnoreCase);
        public bool IsArmor => string.Equals(itemType, "ITEM_ARMOR", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(itemType, "ARMOR", StringComparison.OrdinalIgnoreCase);
        public bool IsCostume => string.Equals(itemType, "ITEM_COSTUME", StringComparison.OrdinalIgnoreCase);
        public bool IsUseItem => string.Equals(itemType, "ITEM_USE", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(itemType, "ITEM_POTION", StringComparison.OrdinalIgnoreCase);

        public Metin2EquipmentSlot EquipmentSlot
        {
            get
            {
                if (IsWeapon) return Metin2EquipmentSlot.Weapon;
                if (IsCostume) return Metin2EquipmentSlot.Costume;
                if (!IsArmor) return Metin2EquipmentSlot.None;
                if (subType.IndexOf("BODY", StringComparison.OrdinalIgnoreCase) >= 0) return Metin2EquipmentSlot.Body;
                if (subType.IndexOf("HEAD", StringComparison.OrdinalIgnoreCase) >= 0) return Metin2EquipmentSlot.Head;
                if (subType.IndexOf("FOOTS", StringComparison.OrdinalIgnoreCase) >= 0 || subType.IndexOf("SHOES", StringComparison.OrdinalIgnoreCase) >= 0) return Metin2EquipmentSlot.Shoes;
                if (subType.IndexOf("WRIST", StringComparison.OrdinalIgnoreCase) >= 0) return Metin2EquipmentSlot.Wrist;
                if (subType.IndexOf("NECK", StringComparison.OrdinalIgnoreCase) >= 0) return Metin2EquipmentSlot.Neck;
                if (subType.IndexOf("EAR", StringComparison.OrdinalIgnoreCase) >= 0) return Metin2EquipmentSlot.Ear;
                if (subType.IndexOf("SHIELD", StringComparison.OrdinalIgnoreCase) >= 0) return Metin2EquipmentSlot.Shield;
                return Metin2EquipmentSlot.None;
            }
        }
    }

    public sealed class Metin2ArmorShapeDefinition
    {
        public string modelResource;
        public string textureResource;
    }

    /// <summary>
    /// Reads the original Metin2 item_list/item_proto tables copied by the editor importer.
    /// No item icon or model name is guessed at runtime.
    /// </summary>
    public static class Metin2ItemDatabase
    {
        const string ResourceRoot = "Metin2Items/";
        static readonly Dictionary<int, Metin2ItemDefinition> definitions = new Dictionary<int, Metin2ItemDefinition>();
        static readonly Dictionary<string, Metin2ArmorShapeDefinition> armorShapes = new Dictionary<string, Metin2ArmorShapeDefinition>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Texture2D> iconCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        static bool loaded;

        public static Metin2ItemDefinition Get(int vnum)
        {
            EnsureLoaded();
            definitions.TryGetValue(vnum, out Metin2ItemDefinition definition);
            return definition;
        }

        public static Texture2D GetIcon(int vnum)
        {
            Metin2ItemDefinition definition = Get(vnum);
            if (definition == null || string.IsNullOrWhiteSpace(definition.iconResource)) return null;
            if (!iconCache.TryGetValue(definition.iconResource, out Texture2D icon))
            {
                icon = Resources.Load<Texture2D>(definition.iconResource);
                iconCache[definition.iconResource] = icon;
            }
            return icon;
        }

        public static GameObject GetWorldModel(int vnum)
        {
            Metin2ItemDefinition definition = Get(vnum);
            return definition == null || string.IsNullOrWhiteSpace(definition.modelResource)
                ? null
                : Resources.Load<GameObject>(definition.modelResource);
        }

        public static Metin2ArmorShapeDefinition GetArmorShape(Metin2CharacterClass characterClass, Metin2Gender gender, int shapeIndex)
        {
            EnsureLoaded();
            string key = characterClass.ToString().ToLowerInvariant() + "_" + (gender == Metin2Gender.Male ? "m" : "w") + "|" + shapeIndex;
            armorShapes.TryGetValue(key, out Metin2ArmorShapeDefinition result);
            return result;
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            ParseItemList(Resources.Load<TextAsset>(ResourceRoot + "item_list"));
            ParseProto(Resources.Load<TextAsset>(ResourceRoot + "item_proto"));
            ParseArmorShapes(Resources.Load<TextAsset>(ResourceRoot + "armor_shapes"));
            if (definitions.Count == 0)
                Debug.LogWarning("Metin2 item catalog is missing. Run Tools > Metin2 > Rebuild Item Assets.");
        }

        static void ParseArmorShapes(TextAsset source)
        {
            if (source == null) return;
            foreach (string raw in Lines(source.text))
            {
                string[] columns = raw.Split('\t');
                if (columns.Length < 4 || !int.TryParse(columns[1], out int shapeIndex)) continue;
                armorShapes[columns[0] + "|" + shapeIndex] = new Metin2ArmorShapeDefinition
                {
                    modelResource = ResourceRoot + "ArmorModels/" + columns[2],
                    textureResource = string.IsNullOrWhiteSpace(columns[3]) ? null : ResourceRoot + "ArmorTextures/" + columns[3]
                };
            }
        }

        static void ParseItemList(TextAsset source)
        {
            if (source == null) return;
            foreach (string raw in Lines(source.text))
            {
                if (string.IsNullOrWhiteSpace(raw) || raw[0] == '#') continue;
                string[] columns = raw.Split('\t');
                if (columns.Length < 3 || !int.TryParse(columns[0].Trim(), out int vnum)) continue;
                Metin2ItemDefinition definition = FindOrCreate(vnum);
                string listType = columns[1].Trim();
                if (string.IsNullOrWhiteSpace(definition.itemType)) definition.itemType = listType;
                definition.iconResource = IconResource(columns[2]);
                if (columns.Length > 3) definition.modelResource = ModelResource(columns[3]);
            }
        }

        static void ParseProto(TextAsset source)
        {
            if (source == null) return;
            foreach (string raw in Lines(source.text))
            {
                string[] columns = raw.Split('\t');
                if (columns.Length < 31 || !int.TryParse(columns[0].Trim(), out int vnum)) continue;
                Metin2ItemDefinition definition = FindOrCreate(vnum);
                definition.itemType = columns[2].Trim();
                definition.subType = columns[3].Trim();
                int.TryParse(columns[4].Trim(), out definition.size);
                definition.size = Mathf.Max(1, definition.size);
                definition.wearFlags = columns[7].Trim();
                for (int index = 0; index < definition.values.Length; index++)
                    int.TryParse(columns[24 + index].Trim(), out definition.values[index]);
            }
        }

        static Metin2ItemDefinition FindOrCreate(int vnum)
        {
            if (!definitions.TryGetValue(vnum, out Metin2ItemDefinition result))
            {
                result = new Metin2ItemDefinition { vnum = vnum };
                definitions.Add(vnum, result);
            }
            return result;
        }

        static IEnumerable<string> Lines(string text)
        {
            using StringReader reader = new StringReader(text ?? string.Empty);
            string line;
            while ((line = reader.ReadLine()) != null) yield return line.TrimEnd('\r');
        }

        static string IconResource(string original)
        {
            string normalized = Normalize(original);
            string file = Path.GetFileNameWithoutExtension(normalized);
            return string.IsNullOrWhiteSpace(file) ? null : ResourceRoot + "Icons/" + file;
        }

        static string ModelResource(string original)
        {
            string normalized = Normalize(original);
            int marker = normalized.IndexOf("/item/", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0) normalized = normalized.Substring(marker + 6);
            normalized = Path.ChangeExtension(normalized, null)?.Trim('/');
            return string.IsNullOrWhiteSpace(normalized) ? null : ResourceRoot + "Models/" + normalized;
        }

        static string Normalize(string value) => (value ?? string.Empty).Trim().Replace('\\', '/');
    }
}
