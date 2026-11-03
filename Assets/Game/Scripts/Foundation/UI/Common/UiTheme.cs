using UnityEngine;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Design tokens for the Canvas UI. Colors come from the legacy USS files (kept so the
    /// migrated screens match the UI Toolkit ones) and the Figma mockup (375x812 portrait).
    /// </summary>
    public static class UiTheme
    {
        /// <summary>Figma frame size; used as the CanvasScaler reference resolution.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(375f, 812f);

        public const float PillHeight = 48f;
        public const float CornerRadius = 24f;

        public static readonly Color Background = Hex("#1A1A1A");
        public static readonly Color BackgroundDeep = Hex("#121212");
        public static readonly Color Surface = Hex("#2C2C2E");
        public static readonly Color SurfaceAlt = Hex("#1C1C1E");
        public static readonly Color Primary = Hex("#403285");
        public static readonly Color Accent = Hex("#5E5CE6");
        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextSecondary = Hex("#8E8E93");

        /// <summary>Approximate, read off the "Xóa chỉ đường" / delete swipe in the mockup. Refine when those screens land.</summary>
        public static readonly Color Danger = Hex("#E5484D");

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
        }
    }
}
