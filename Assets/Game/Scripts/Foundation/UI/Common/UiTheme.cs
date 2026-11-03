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

        // Phase 1 values are read straight from the Figma exports (raw sRGB pixels of the Splash and
        // Welcome frames); the rest still comes from the legacy USS until its screen is migrated.
        public static readonly Color Background = Hex("#191919");
        public static readonly Color BackgroundDeep = Hex("#121212");
        public static readonly Color Surface = Hex("#2B2B2B");
        public static readonly Color SurfaceAlt = Hex("#1C1C1E");
        public static readonly Color Primary = Hex("#3B3189");
        public static readonly Color Accent = Hex("#5E5CE6");
        public static readonly Color TextPrimary = Color.white;
        public static readonly Color TextTitle = Hex("#F3F3F3");
        public static readonly Color TextBody = Hex("#C4C4C4");
        public static readonly Color TextSecondary = Hex("#8E8E93");

        /// <summary>Approximate, read off the "Xóa chỉ đường" / delete swipe in the mockup. Refine when those screens land.</summary>
        public static readonly Color Danger = Hex("#E5484D");

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
        }
    }
}
