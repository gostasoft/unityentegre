#if UNITY_EDITOR
using Metin2Dev.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Metin2Dev.Editor
{
    [InitializeOnLoad]
    static class Metin2PlayerMovementSettingsAsset
    {
        const string Folder = "Assets/Metin2/Gameplay/Generated/Resources";
        const string AssetPath = Folder + "/Metin2PlayerMovementSettings.asset";

        static Metin2PlayerMovementSettingsAsset()
        {
            EditorApplication.delayCall += EnsureAsset;
        }

        static void EnsureAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<Metin2PlayerMovementSettings>(AssetPath) != null) return;
            string current = "Assets";
            foreach (string part in new[] { "Metin2", "Gameplay", "Generated", "Resources" })
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Metin2PlayerMovementSettings>(), AssetPath);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
