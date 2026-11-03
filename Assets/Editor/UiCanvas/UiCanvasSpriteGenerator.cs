#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Writes the two procedural base sprites every Canvas screen reuses: a 9-sliced rounded rectangle
    /// (buttons, cards, inputs, chips, chat bubbles) and a circle. They are white so Image.color tints them.
    /// </summary>
    public static class UiCanvasSpriteGenerator
    {
        private const int RoundedSize = 64;
        private const int CircleSize = 128;

        [MenuItem("Tools/UI Canvas/Generate Base Sprites")]
        public static void Generate()
        {
            Directory.CreateDirectory(UiCanvasPaths.CommonArts);
            WritePng(UiCanvasPaths.RoundedSprite, RoundedSize, RoundedRectAlpha);
            WritePng(UiCanvasPaths.CircleSprite, CircleSize, CircleAlpha);
            AssetDatabase.Refresh();
            Debug.Log("[UiCanvas] Base sprites generated in " + UiCanvasPaths.CommonArts);
        }

        private static float RoundedRectAlpha(float x, float y)
        {
            float half = RoundedSize * 0.5f;
            float radius = UiCanvasPaths.RoundedCornerPixels;
            float qx = Mathf.Abs(x - half) - (half - radius);
            float qy = Mathf.Abs(y - half) - (half - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
            return Mathf.Clamp01(0.5f - distance);
        }

        private static float CircleAlpha(float x, float y)
        {
            float half = CircleSize * 0.5f;
            float distance = new Vector2(x - half, y - half).magnitude - (half - 1f);
            return Mathf.Clamp01(0.5f - distance);
        }

        private static void WritePng(string path, int size, System.Func<float, float, float> alphaAt)
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
            Object.DestroyImmediate(texture);
        }
    }
}
#endif
