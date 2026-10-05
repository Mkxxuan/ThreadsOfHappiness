using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Toh.Editor
{
    /// <summary>
    /// 打开工程时自动把 Resources/Art 下的图片导入为 Sprite，
    /// 使运行时 Resources.Load&lt;Sprite&gt; 可用（美术素材由外部管线生成）。
    /// </summary>
    [InitializeOnLoad]
    public static class ArtImportSetup
    {
        const string ArtRoot = "Assets/Resources/Art";

        static ArtImportSetup()
        {
            EditorApplication.delayCall += FixImports;
        }

        static void FixImports()
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot)) return;
            bool changed = false;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureType == TextureImporterType.Sprite) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                changed = true;
            }
            if (changed) Debug.Log("[ArtImportSetup] Art textures reimported as Sprite.");
        }
    }
}
