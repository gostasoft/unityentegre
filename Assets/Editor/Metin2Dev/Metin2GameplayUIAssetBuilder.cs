#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Metin2Dev.Editor
{
    public static class Metin2GameplayUIAssetBuilder
    {
        const string Destination = "Assets/Metin2/UI/Resources/Metin2UI";

        static readonly string[] SourceFiles =
        {
            "taskbar.tga",
            "public.dds",
            "windows.dds",
            "skillwarrior.dds",
            "skillassassin.dds",
            "skillsura.dds",
            "skillshaman.dds",
            "pattern/taskbar_base.tga",
            "pattern/board_base.tga",
            "pattern/board_corner_lefttop.tga",
            "pattern/board_corner_righttop.tga",
            "pattern/board_corner_leftbottom.tga",
            "pattern/board_corner_rightbottom.tga",
            "pattern/board_line_top.tga",
            "pattern/board_line_bottom.tga",
            "pattern/board_line_left.tga",
            "pattern/board_line_right.tga",
            "pattern/titlebar_left.tga",
            "pattern/titlebar_center.tga",
            "pattern/titlebar_right.tga",
            "pattern/gauge_red.tga",
            "pattern/gauge_blue.tga",
            "pattern/gauge_pink.tga",
            "pattern/gauge_purple.tga",
            "pattern/horizontalbar_left.tga",
            "pattern/horizontalbar_center.tga",
            "pattern/horizontalbar_right.tga",
            "pattern/HPGauge/01.tga|hp_gauge_01.tga",
            "pattern/SPGauge/01.tga|sp_gauge_01.tga",
            "pattern/STGauge/01.tga|st_gauge_01.tga",
        };

        [InitializeOnLoadMethod]
        static void QueueFirstBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(Path.Combine(Destination, "taskbar.tga")) ||
                    !File.Exists(Path.Combine(Destination, "hp_gauge_01.tga"))) Build();
            };
        }

        [MenuItem("Tools/Metin2/Build Gameplay UI Assets")]
        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string sourceRoot = Path.Combine(projectRoot, "Metin2,5", "Extracted", "ETC", "ymir work", "ui");
            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogError("[Metin2UI] Source UI directory is missing: " + sourceRoot);
                return;
            }

            Directory.CreateDirectory(Path.Combine(projectRoot, Destination));
            List<string> missing = new List<string>();
            foreach (string entry in SourceFiles)
            {
                string[] parts = entry.Split('|');
                string relative = parts[0];
                string source = Path.Combine(sourceRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(source))
                {
                    missing.Add(relative);
                    continue;
                }
                string outputName = parts.Length > 1 ? parts[1] : Path.GetFileName(source);
                string destination = Path.Combine(projectRoot, Destination, outputName);
                File.Copy(source, destination, true);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string entry in SourceFiles)
            {
                string[] parts = entry.Split('|');
                string relative = parts[0];
                string outputName = parts.Length > 1 ? parts[1] : Path.GetFileName(relative);
                string assetPath = Destination + "/" + outputName;
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                string importName = Path.GetFileName(relative);
                importer.wrapMode = importName.Equals("board_base.tga", StringComparison.OrdinalIgnoreCase) ||
                                    importName.StartsWith("board_line_", StringComparison.OrdinalIgnoreCase) ||
                                    importName.Equals("titlebar_center.tga", StringComparison.OrdinalIgnoreCase) ||
                                    importName.Equals("horizontalbar_center.tga", StringComparison.OrdinalIgnoreCase)
                    ? TextureWrapMode.Repeat
                    : TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            if (missing.Count > 0) Debug.LogWarning("[Metin2UI] Missing source textures: " + string.Join(", ", missing));
            Debug.Log("[Metin2UI] Original gameplay UI atlases prepared: " + (SourceFiles.Length - missing.Count) + "/" + SourceFiles.Length + ".");
        }
    }
}
#endif
