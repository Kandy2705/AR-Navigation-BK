#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Builds dynamic TMP font assets from the Poppins TTFs the UI Toolkit screens already use.
    /// Poppins has no glyphs for most Vietnamese vowels (ơ, ư, ạ, ả, ế, ệ ...), so each asset falls back
    /// to Liberation Sans, which covers them. Swap the fallback once a final Vietnamese font is chosen.
    /// </summary>
    public static class UiCanvasFontGenerator
    {
        [MenuItem("Tools/UI Canvas/Generate Poppins TMP Fonts")]
        public static void Generate()
        {
            Directory.CreateDirectory(UiCanvasPaths.Fonts);
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiCanvasPaths.TmpFallbackFont);
            if (fallback == null)
            {
                Debug.LogWarning("[UiCanvas] Fallback font not found at " + UiCanvasPaths.TmpFallbackFont + "; Vietnamese glyphs will be missing.");
            }

            foreach (string weight in UiCanvasPaths.PoppinsWeights)
            {
                CreateIfMissing(weight, fallback);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[UiCanvas] Poppins TMP font assets ready in " + UiCanvasPaths.Fonts);
        }

        private static void CreateIfMissing(string weight, TMP_FontAsset fallback)
        {
            string assetPath = UiCanvasPaths.PoppinsFontAsset(weight);
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
            {
                return;
            }

            string sourcePath = UiCanvasPaths.PoppinsSourceFolder + "/Poppins-" + weight + ".ttf";
            var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                Debug.LogError("[UiCanvas] Missing source font " + sourcePath);
                return;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = "Poppins-" + weight + " SDF";
            AssetDatabase.CreateAsset(asset, assetPath);

            if (asset.material != null)
            {
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            foreach (Texture2D atlas in asset.atlasTextures)
            {
                atlas.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, asset);
            }

            if (fallback != null)
            {
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            }

            EditorUtility.SetDirty(asset);
        }
    }
}
#endif
