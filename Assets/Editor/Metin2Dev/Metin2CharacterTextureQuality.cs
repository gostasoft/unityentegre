#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Metin2Dev.Editor
{
    [InitializeOnLoad]
    static class Metin2CharacterTextureQuality
    {
        const string CharacterRoot = "Assets/Metin2/Frontend/Art/Characters";

        static Metin2CharacterTextureQuality()
        {
            EditorApplication.delayCall += Upgrade;
        }

        [MenuItem("Tools/Metin2/Upgrade Character Texture Quality")]
        static void Upgrade()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CharacterRoot }))
            {
                TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (importer == null) continue;
                bool changed = importer.maxTextureSize != 4096 || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 16 || !importer.mipmapEnabled;
                if (!changed) continue;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.compressionQuality = 100;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 16;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
