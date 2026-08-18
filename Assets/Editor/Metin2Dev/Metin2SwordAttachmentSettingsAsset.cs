#if UNITY_EDITOR
using Metin2Dev.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Metin2Dev.Editor
{
    [InitializeOnLoad]
    static class Metin2SwordAttachmentSettingsAsset
    {
        const string Folder = "Assets/Metin2/Gameplay/Generated/Resources";
        const string AssetPath = Folder + "/Metin2SwordAttachmentSettings.asset";

        static Metin2SwordAttachmentSettingsAsset()
        {
            EditorApplication.delayCall += EnsureAsset;
        }

        static void EnsureAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<Metin2SwordAttachmentSettings>(AssetPath) != null) return;
            string current = "Assets";
            foreach (string part in new[] { "Metin2", "Gameplay", "Generated", "Resources" })
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Metin2SwordAttachmentSettings>(), AssetPath);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
