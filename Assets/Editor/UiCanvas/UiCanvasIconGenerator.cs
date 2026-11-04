#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Line icons for the history, settings and contact screens, drawn as white strokes on a 24 grid (rendered at
    /// 48 px) so Image.color tints them. They stand in for the Figma icons: export those over the same file names
    /// and nothing else has to change.
    /// </summary>
    public static class UiCanvasIconGenerator
    {
        public const string ChevronRight = "ui_icon_chevron_right.png";
        public const string Search = "ui_icon_search.png";
        public const string Trash = "ui_icon_trash.png";
        public const string Phone = "ui_icon_nav_phone.png";
        public const string Clock = "ui_icon_nav_clock.png";
        public const string Gear = "ui_icon_nav_gear.png";
        public const string Send = "ui_icon_send.png";
        public const string Cross = "ui_icon_cross.png";
        public const string User = "ui_icon_user_line.png";
        public const string Help = "ui_icon_help_ring.png";
        public const string Doc = "ui_icon_doc.png";
        public const string Logout = "ui_icon_logout.png";
        public const string Pin = "ui_icon_pin.png";
        public const string Handset = "ui_icon_handset.png";

        private const int Size = 48;
        private const float Grid = 24f;
        private const float HalfStroke = 1.1f;

        [MenuItem("Tools/UI Canvas/Generate Icon Sprites")]
        public static void Generate()
        {
            Directory.CreateDirectory(UiCanvasPaths.CommonArts);

            Write(ChevronRight, Lines((8, 4, 16, 12), (16, 12, 8, 20)));
            Write(Search, Min(Ring(10.5f, 10.5f, 6.5f), Lines((15.2f, 15.2f, 20f, 20f))));
            Write(Trash, Lines(
                (4, 7, 20, 7), (9, 7, 9, 4.5f), (9, 4.5f, 15, 4.5f), (15, 4.5f, 15, 7),
                (6.5f, 7, 7.5f, 20), (7.5f, 20, 16.5f, 20), (16.5f, 20, 17.5f, 7),
                (10.5f, 10.5f, 10.5f, 16.5f), (13.5f, 10.5f, 13.5f, 16.5f)));
            Write(Phone, Lines(
                (7, 4, 17, 4), (17, 4, 17, 20), (17, 20, 7, 20), (7, 20, 7, 4), (11, 17.5f, 13, 17.5f)));
            Write(Clock, Min(Ring(12, 12, 9), Lines((12, 7, 12, 12), (12, 12, 15.5f, 14))));
            Write(Gear, Min(Ring(12, 12, 6.5f), Ring(12, 12, 2.5f), Teeth(12, 12, 8f, 10.5f, 8)));
            Write(Send, Lines((3, 11, 21, 3), (21, 3, 14.5f, 21), (14.5f, 21, 11, 13), (11, 13, 3, 11), (11, 13, 21, 3)));
            Write(Cross, Lines((6, 6, 18, 18), (18, 6, 6, 18)));
            Write(User, Min(Ring(12, 7.5f, 3.5f), Lines((5, 20, 5, 17.5f), (5, 17.5f, 8, 14), (8, 14, 16, 14), (16, 14, 19, 17.5f), (19, 17.5f, 19, 20), (5, 20, 19, 20))));
            Write(Help, Ring(12, 12, 9.5f));
            Write(Doc, Lines((6, 3, 14, 3), (14, 3, 18, 7), (18, 7, 18, 21), (18, 21, 6, 21), (6, 21, 6, 3)));
            Write(Logout, Lines((10, 4, 5, 4), (5, 4, 5, 20), (5, 20, 10, 20), (9, 12, 20, 12), (16, 8, 20, 12), (20, 12, 16, 16)));
            Write(Pin, Min(Ring(12, 9.5f, 4), Lines((7.5f, 12, 12, 21), (12, 21, 16.5f, 12))));
            Write(Handset, Lines(
                (5, 4, 9, 4), (9, 4, 10.5f, 8.5f), (10.5f, 8.5f, 8, 10), (8, 10, 14, 16), (14, 16, 15.5f, 13.5f),
                (15.5f, 13.5f, 20, 15), (20, 15, 20, 19), (20, 19, 16, 20), (16, 20, 5, 9), (5, 9, 5, 4)));

            AssetDatabase.Refresh();
            Debug.Log("[UiCanvas] Icon sprites generated in " + UiCanvasPaths.CommonArts);
        }

        private delegate float Distance(float x, float y);

        private static void Write(string fileName, Distance distanceToStroke)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            float scale = Grid / Size;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Texture y points up, the 24 grid is drawn y-down.
                    float gx = (x + 0.5f) * scale;
                    float gy = (Size - y - 0.5f) * scale;
                    float alpha = Mathf.Clamp01((HalfStroke - distanceToStroke(gx, gy)) / scale + 0.5f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(UiCanvasPaths.CommonArt(fileName), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static Distance Lines(params (float ax, float ay, float bx, float by)[] segments)
        {
            return (x, y) =>
            {
                float best = float.MaxValue;
                foreach (var s in segments)
                {
                    best = Mathf.Min(best, ToSegment(x, y, s.ax, s.ay, s.bx, s.by));
                }

                return best;
            };
        }

        private static Distance Ring(float cx, float cy, float radius)
        {
            return (x, y) => Mathf.Abs(new Vector2(x - cx, y - cy).magnitude - radius);
        }

        /// <summary>Short radial strokes around a center (the teeth of a gear).</summary>
        private static Distance Teeth(float cx, float cy, float inner, float outer, int count)
        {
            var segments = new (float, float, float, float)[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                segments[i] = (cx + cos * inner, cy + sin * inner, cx + cos * outer, cy + sin * outer);
            }

            return Lines(segments);
        }

        private static Distance Min(params Distance[] parts)
        {
            return (x, y) =>
            {
                float best = float.MaxValue;
                foreach (Distance part in parts)
                {
                    best = Mathf.Min(best, part(x, y));
                }

                return best;
            };
        }

        private static float ToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            var p = new Vector2(px, py);
            var a = new Vector2(ax, ay);
            Vector2 ab = new Vector2(bx, by) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (p - (a + ab * t)).magnitude;
        }
    }
}
#endif
