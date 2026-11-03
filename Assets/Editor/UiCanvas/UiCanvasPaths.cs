#if UNITY_EDITOR
namespace Game.Foundation.UI.EditorTools
{
    /// <summary>
    /// Asset locations shared by the Canvas UI generators, import processor and tests.
    /// Layout follows the sun-studio reference project: one folder per screen under
    /// Bundles/UI/&lt;Name&gt;UI/{Prefabs,Arts}, shared pieces under Bundles/UI/Common.
    /// </summary>
    public static class UiCanvasPaths
    {
        public const string UiBundles = "Assets/Game/Bundles/UI";
        public const string CommonArts = UiBundles + "/Common/Arts";
        public const string CommonPrefabs = UiBundles + "/Common/Prefabs";
        public const string Fonts = "Assets/Game/Bundles/Fonts/Game";

        public const string RoundedSprite = CommonArts + "/ui_rounded_9slice.png";
        public const string CircleSprite = CommonArts + "/ui_circle.png";
        public const string AppCanvasPrefab = CommonPrefabs + "/AppCanvas.prefab";

        /// <summary>Source TTFs imported by the legacy UI Toolkit screens.</summary>
        public const string PoppinsSourceFolder = "Assets/UI/Fonts";

        public const string TmpFallbackFont = "Assets/Plugins/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        public static readonly string[] PoppinsWeights = { "Regular", "Medium", "SemiBold", "Bold" };

        public static string PoppinsFontAsset(string weight)
        {
            return Fonts + "/Poppins-" + weight + " SDF.asset";
        }

        /// <summary>True for textures that live in a screen's (or Common's) Arts folder.</summary>
        public static bool IsUiArt(string assetPath)
        {
            return assetPath.StartsWith(UiBundles + "/") && assetPath.Contains("/Arts/");
        }

        /// <summary>Corner size of <see cref="RoundedSprite"/>; also its 9-slice border.</summary>
        public const int RoundedCornerPixels = 24;
    }
}
#endif
