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
    /// to our own Liberation Sans asset, which covers them. Swap the fallback once a final Vietnamese font is chosen.
    ///
    /// All assets use the standard "TextMeshPro/Distance Field" shader. TMP picks the "Mobile/Distance Field"
    /// variant for Android targets, and in this Unity version (6000.3, Metal editor) it draws a faint smeared
    /// foot under every glyph, with any font and any camera. The fallback has to use the same shader or the
    /// glyphs drawn through it show the artifact.
    /// </summary>
    public static class UiCanvasFontGenerator
    {
        [MenuItem("Tools/UI Canvas/Generate Poppins TMP Fonts")]
        public static void Generate()
        {
            Directory.CreateDirectory(UiCanvasPaths.Fonts);

            TMP_FontAsset fallback = EnsureFont(UiCanvasPaths.FallbackFontAsset, UiCanvasPaths.FallbackSourceTtf, "LiberationSans-Fallback SDF", null);
            foreach (string weight in UiCanvasPaths.PoppinsWeights)
            {
                EnsureFont(
                    UiCanvasPaths.PoppinsFontAsset(weight),
                    UiCanvasPaths.PoppinsSourceFolder + "/Poppins-" + weight + ".ttf",
                    "Poppins-" + weight + " SDF",
                    fallback);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[UiCanvas] Poppins TMP font assets ready in " + UiCanvasPaths.Fonts);
        }

        /// <summary>Creates the asset when missing, then (re)applies the shader and fallback so old assets are repaired too.</summary>
        private static TMP_FontAsset EnsureFont(string assetPath, string sourcePath, string assetName, TMP_FontAsset fallback)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
                if (source == null)
                {
                    Debug.LogError("[UiCanvas] Missing source font " + sourcePath);
                    return null;
                }

                asset = TMP_FontAsset.CreateFontAsset(
                    source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                asset.name = assetName;
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
            }

            Shader sdf = Shader.Find(UiCanvasPaths.SdfShaderName);
            if (sdf != null && asset.material != null && asset.material.shader != sdf)
            {
                asset.material.shader = sdf;
                EditorUtility.SetDirty(asset.material);
            }

            var wanted = new List<TMP_FontAsset>();
            if (fallback != null)
            {
                wanted.Add(fallback);
            }

            if (!SameList(asset.fallbackFontAssetTable, wanted))
            {
                asset.fallbackFontAssetTable = wanted;
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static bool SameList(List<TMP_FontAsset> a, List<TMP_FontAsset> b)
        {
            if (a == null || a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
