using System.IO;
using Metin3Dev.Panel;
using UnityEditor;
using UnityEngine;

namespace Metin3Dev.PanelEditor
{
    public static class Metin3PanelConnectionEditor
    {
        const string Folder = "Assets/Metin3Panel/Resources";
        const string AssetPath = Folder + "/Metin3PanelConnection.asset";

        [MenuItem("Tools/Metin3/Panel/Create or Select Connection Settings")]
        public static void CreateOrSelect()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            Metin3PanelConnection settings = AssetDatabase.LoadAssetAtPath<Metin3PanelConnection>(AssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<Metin3PanelConnection>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Metin3 Panel] Bağlantı ayarı oluşturuldu. Yayın adresini Inspector üzerinden seçebilirsin.");
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
