using UnityEditor;
using UnityEngine;

namespace DeepDwarf.EditorTools
{
    /// <summary>Every PNG in Resources/Art gets crisp pixel-art import settings automatically.</summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        const string ArtFolder = "Assets/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder)) return;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = name == "dark" ? 8 : 24;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = name.StartsWith("dwarf_") || name == "shop"
                ? (int)SpriteAlignment.BottomCenter
                : (int)SpriteAlignment.Center;
            settings.spriteBorder = name.StartsWith("ui_") && name != "ui_arrow" && name != "ui_white"
                ? new Vector4(4, 4, 4, 4)
                : Vector4.zero;
            importer.SetTextureSettings(settings);
        }
    }
}
