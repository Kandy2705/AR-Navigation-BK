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

        // Navigation HUD (the B9 guidance overlay).
        public const string Refresh = "ui_icon_refresh.png";
        public const string ExitDoor = "ui_icon_exit_door.png";
        public const string Flag = "ui_icon_flag.png";
        public const string Record = "ui_icon_record.png";
        public const string Download = "ui_icon_download.png";
        public const string Flask = "ui_icon_flask.png";
        public const string Navigate = "ui_icon_navigate.png";

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

            Write(Refresh, Min(Arc(12, 12, 7.5f, 50f, 330f), ArrowHead(12, 12, 7.5f, 50f)));
            Write(ExitDoor, Lines(
                (13, 7, 13, 3), (13, 3, 4, 3), (4, 3, 4, 21), (4, 21, 13, 21), (13, 21, 13, 17),
                (9, 12, 21, 12), (17, 8, 21, 12), (21, 12, 17, 16)));
            Write(Flag, Lines((6, 21, 6, 3), (6, 4, 18, 4), (18, 4, 15, 8), (15, 8, 18, 12), (18, 12, 6, 12)));
            Write(Record, Min(Ring(12, 12, 8.5f), Disk(12, 12, 4.5f)));
            Write(Download, Lines((12, 3, 12, 15), (7, 10, 12, 15), (17, 10, 12, 15), (4, 17, 4, 20), (4, 20, 20, 20), (20, 20, 20, 17)));
            Write(Flask, Lines(
                (9, 3, 15, 3), (10, 3, 10, 9), (14, 3, 14, 9), (10, 9, 4.5f, 19), (14, 9, 19.5f, 19),
                (4.5f, 19, 5.5f, 21), (5.5f, 21, 18.5f, 21), (18.5f, 21, 19.5f, 19), (7, 15, 17, 15)));
            Write(Navigate, Lines((12, 3, 19, 20), (19, 20, 12, 16), (12, 16, 5, 20), (5, 20, 12, 3)));

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

        /// <summary>Filled circle: inside counts as on the stroke.</summary>
        private static Distance Disk(float cx, float cy, float radius)
        {
            return (x, y) => Mathf.Max(0f, new Vector2(x - cx, y - cy).magnitude - radius + HalfStroke);
        }

        /// <summary>Part of a circle from <paramref name="fromDegrees"/> to <paramref name="toDegrees"/> (clockwise on screen, 0 = right).</summary>
        private static Distance Arc(float cx, float cy, float radius, float fromDegrees, float toDegrees)
        {
            return (x, y) =>
            {
                float angle = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;
                if (angle < 0f)
                {
                    angle += 360f;
                }

                if (angle >= fromDegrees && angle <= toDegrees)
                {
                    return Mathf.Abs(new Vector2(x - cx, y - cy).magnitude - radius);
                }

                return Mathf.Min(ToPoint(x, y, cx, cy, radius, fromDegrees), ToPoint(x, y, cx, cy, radius, toDegrees));
            };
        }

        /// <summary>Arrow tip at the start of an <see cref="Arc"/>, pointing against the arc's direction.</summary>
        private static Distance ArrowHead(float cx, float cy, float radius, float atDegrees)
        {
            float a = atDegrees * Mathf.Deg2Rad;
            var tip = new Vector2(cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius);
            var tangent = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)); // towards smaller angles
            var normal = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 back = tip - tangent * 4f;
            Vector2 left = back + normal * 3.5f;
            Vector2 right = back - normal * 3.5f;
            return Lines((tip.x, tip.y, left.x, left.y), (tip.x, tip.y, right.x, right.y));
        }

        private static float ToPoint(float x, float y, float cx, float cy, float radius, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(x - (cx + Mathf.Cos(a) * radius), y - (cy + Mathf.Sin(a) * radius)).magnitude;
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
