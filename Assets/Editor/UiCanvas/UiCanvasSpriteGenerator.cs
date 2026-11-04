#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Writes the procedural sprites every Canvas screen reuses. They are white so Image.color tints them:
    /// 9-sliced rounded rectangles (fills and outlines at the radii the Figma uses), a circle and a back arrow.
    /// </summary>
    public static class UiCanvasSpriteGenerator
    {
        [MenuItem("Tools/UI Canvas/Generate Base Sprites")]
        public static void Generate()
        {
            Directory.CreateDirectory(UiCanvasPaths.CommonArts);

            // Original pill/button shape and circle (Phase 0).
            WritePng(UiCanvasPaths.RoundedSprite, 64, (x, y) => RoundedAlpha(x, y, 64, UiCanvasPaths.RoundedCornerPixels, 0f));
            WritePng(UiCanvasPaths.CircleSprite, 128, (x, y) => CircleAlpha(x, y, 128));

            // Auth screens (Phase 2): input cards, OTP boxes, checkbox.
            WritePng(UiCanvasPaths.CommonArt(UiCanvasPaths.RoundedR16File), 48, (x, y) => RoundedAlpha(x, y, 48, 16f, 0f));
            WritePng(UiCanvasPaths.CommonArt(UiCanvasPaths.OutlineR16File), 48, (x, y) => RoundedAlpha(x, y, 48, 16f, 1f));
            WritePng(UiCanvasPaths.CommonArt(UiCanvasPaths.CheckboxFillFile), 24, (x, y) => RoundedAlpha(x, y, 24, 6f, 0f));
            WritePng(UiCanvasPaths.CommonArt(UiCanvasPaths.CheckboxOutlineFile), 24, (x, y) => RoundedAlpha(x, y, 24, 6f, 2f));
            WritePng(UiCanvasPaths.CommonArt(UiCanvasPaths.BackIconFile), 24, BackArrowAlpha);

            // Register screens (Phase 3): avatar tile and the dropdown chevron.
            WritePng(UiCanvasPaths.CommonArt(UiCanvasPaths.RoundedR32File), 96, (x, y) => RoundedAlpha(x, y, 96, 32f, 0f));
            WritePng(UiCanvasPaths.CommonArt(UiCanvasPaths.ChevronDownFile), 24, ChevronDownAlpha);

            AssetDatabase.Refresh();
            Debug.Log("[UiCanvas] Base sprites generated in " + UiCanvasPaths.CommonArts);
        }

        /// <summary>Rounded square of <paramref name="size"/>; stroke 0 is a fill, otherwise an inner outline of that width.</summary>
        private static float RoundedAlpha(float x, float y, int size, float radius, float stroke)
        {
            float half = size * 0.5f;
            float qx = Mathf.Abs(x - half) - (half - radius);
            float qy = Mathf.Abs(y - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius; // negative inside

            if (stroke <= 0f)
            {
                return Mathf.Clamp01(0.5f - distance);
            }

            return Mathf.Clamp01(0.5f - Mathf.Max(distance, -(distance + stroke)));
        }

        private static float CircleAlpha(float x, float y, int size)
        {
            float half = size * 0.5f;
            float distance = new Vector2(x - half, y - half).magnitude - (half - 1f);
            return Mathf.Clamp01(0.5f - distance);
        }

        /// <summary>Left arrow (shaft plus head) on a 24px grid, 2.2px stroke, round-ish ends. Placeholder until the Figma icon is exported.</summary>
        private static float BackArrowAlpha(float x, float y)
        {
            const float halfStroke = 1.1f;
            float distance = Mathf.Min(
                DistanceToSegment(x, y, 4f, 12f, 20f, 12f),
                Mathf.Min(DistanceToSegment(x, y, 4f, 12f, 11f, 5f), DistanceToSegment(x, y, 4f, 12f, 11f, 19f)));
            return Mathf.Clamp01(0.5f + halfStroke - distance);
        }

        /// <summary>Down chevron on a 24px grid, 2.2px stroke. Placeholder until the Figma icon is exported.</summary>
        private static float ChevronDownAlpha(float x, float y)
        {
            const float halfStroke = 1.1f;
            // The texture's y axis points up, so the chevron's tip is the lowest point.
            float distance = Mathf.Min(DistanceToSegment(x, y, 5f, 14f, 12f, 8f), DistanceToSegment(x, y, 12f, 8f, 19f, 14f));
            return Mathf.Clamp01(0.5f + halfStroke - distance);
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(px, py);
            var a = new Vector2(ax, ay);
            var b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        private static void WritePng(string path, int size, Func<float, float, float> alphaAt)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte alpha = (byte)Mathf.RoundToInt(alphaAt(x + 0.5f, y + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
#endif
