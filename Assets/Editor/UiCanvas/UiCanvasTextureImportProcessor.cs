#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Any texture in Assets/Game/Bundles/UI/**/Arts imports as a crisp UI sprite, so exported Figma PNGs
    /// need no manual import settings. The rounded base sprite also gets its 9-slice border.
    /// </summary>
    public class UiCanvasTextureImportProcessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!UiCanvasPaths.IsUiArt(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100f;

            int border = UiCanvasPaths.NineSliceBorder(System.IO.Path.GetFileName(assetPath));
            if (border > 0)
            {
                importer.spriteBorder = new Vector4(border, border, border, border);
            }
        }
    }
}
#endif
