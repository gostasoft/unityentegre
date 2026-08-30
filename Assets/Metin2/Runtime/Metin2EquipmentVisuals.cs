using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Metin2Dev.Frontend;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2EquipmentVisuals : MonoBehaviour
    {
        const int LocalPlayerLayer = 8;
        Transform characterVisual;
        Transform rightHand;
        GameObject weaponObject;
        GameObject armorObject;
        SkinnedMeshRenderer[] baseBodyRenderers;

        void Awake()
        {
            characterVisual = transform.Find("Character Visual") ?? transform;
            rightHand = FindDeep(characterVisual, "Bip01 R Hand") ?? FindDeep(characterVisual, "equip_right_hand");
            baseBodyRenderers = characterVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => !IsUnderNamed(renderer.transform, "Character Hair") && !IsUnderNamed(renderer.transform, "Weapon -"))
                .ToArray();
        }

        void OnEnable()
        {
            Metin2InventoryService.EquippedChanged += Refresh;
            Refresh();
        }

        void OnDisable() => Metin2InventoryService.EquippedChanged -= Refresh;

        public void Refresh()
        {
            RefreshWeapon();
            RefreshArmor();
        }

        void RefreshWeapon()
        {
            Metin2InventoryEntry weapon = Metin2InventoryService.GetEquipped(Metin2EquipmentSlot.Weapon);
            if (weaponObject != null) Destroy(weaponObject);
            foreach (Transform child in characterVisual.GetComponentsInChildren<Transform>(true)
                         .Where(item => item != null && item.name.StartsWith("Weapon -", StringComparison.Ordinal)).ToArray())
                if (child != null) Destroy(child.gameObject);
            if (weapon == null || rightHand == null) return;
            GameObject prefab = Metin2ItemDatabase.GetWorldModel(weapon.vnum);
            if (prefab == null)
            {
                Debug.LogWarning($"Metin2 weapon FBX is not available for VNUM {weapon.vnum}.");
                return;
            }
            weaponObject = Instantiate(prefab, rightHand, false);
            weaponObject.name = $"Weapon - {weapon.vnum:D5} ({weapon.name})";
            Metin2SwordAttachmentSettings settings = Resources.Load<Metin2SwordAttachmentSettings>("Metin2SwordAttachmentSettings");
            weaponObject.transform.SetLocalPositionAndRotation(
                settings != null ? settings.LocalPosition : Vector3.zero,
                Quaternion.Euler(settings != null ? settings.LocalEulerAngles : new Vector3(0f, 0f, 90f)));
            weaponObject.transform.localScale = settings != null ? settings.localScale : Vector3.one;
            SetLayer(weaponObject.transform, LocalPlayerLayer);
            foreach (Camera nested in weaponObject.GetComponentsInChildren<Camera>(true)) nested.enabled = false;
            foreach (Light nested in weaponObject.GetComponentsInChildren<Light>(true)) nested.enabled = false;
        }

        void RefreshArmor()
        {
            if (armorObject != null) Destroy(armorObject);
            foreach (SkinnedMeshRenderer renderer in baseBodyRenderers) if (renderer != null) renderer.enabled = true;
            Metin2InventoryEntry armor = Metin2InventoryService.GetEquipped(Metin2EquipmentSlot.Body);
            Metin2ItemDefinition item = armor != null ? Metin2ItemDatabase.Get(armor.vnum) : null;
            if (item == null) return;
            Metin2ArmorShapeDefinition shape = Metin2ItemDatabase.GetArmorShape(
                Metin2GameplaySession.CharacterClass, Metin2GameplaySession.Gender, item.values[3]);
            GameObject prefab = shape != null ? Resources.Load<GameObject>(shape.modelResource) : null;
            if (prefab == null) return;

            armorObject = Instantiate(prefab, characterVisual, false);
            armorObject.name = $"Equipped Armor - {armor.vnum} (Shape {item.values[3]})";
            armorObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            armorObject.transform.localScale = Vector3.one;
            foreach (Animator nested in armorObject.GetComponentsInChildren<Animator>(true)) nested.enabled = false;

            Dictionary<string, Transform> bones = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (Transform bone in characterVisual.GetComponentsInChildren<Transform>(true))
                if (!IsUnderNamed(bone, "Equipped Armor -") && !bones.ContainsKey(bone.name)) bones.Add(bone.name, bone);
            Texture2D targetTexture = !string.IsNullOrWhiteSpace(shape.textureResource)
                ? Resources.Load<Texture2D>(shape.textureResource) : null;
            foreach (SkinnedMeshRenderer renderer in armorObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] rebound = new Transform[renderer.bones.Length];
                for (int index = 0; index < rebound.Length; index++)
                    rebound[index] = renderer.bones[index] != null && bones.TryGetValue(renderer.bones[index].name, out Transform match)
                        ? match : renderer.bones[index];
                renderer.bones = rebound;
                if (renderer.rootBone != null && bones.TryGetValue(renderer.rootBone.name, out Transform root)) renderer.rootBone = root;
                if (targetTexture != null)
                {
                    Material[] materials = renderer.materials;
                    foreach (Material material in materials) if (material != null) material.mainTexture = targetTexture;
                    renderer.materials = materials;
                }
            }
            foreach (SkinnedMeshRenderer renderer in baseBodyRenderers) if (renderer != null) renderer.enabled = false;
            SetLayer(armorObject.transform, LocalPlayerLayer);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }

        static bool IsUnderNamed(Transform item, string prefix)
        {
            for (Transform current = item; current != null; current = current.parent)
                if (current.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int index = 0; index < root.childCount; index++) SetLayer(root.GetChild(index), layer);
        }
    }
}
