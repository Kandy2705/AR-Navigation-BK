using System.Collections.Generic;

/// <summary>
/// Standalone GPS scene boots outdoor HUD stacks via Unity <c>RuntimeInitializeOnLoadMethod</c> hooks.
/// <see cref="HybridGpsMapSceneName"/> relies on Hierarchy + <see cref="HybridOutdoorNavigationRoot"/> instead.
/// </summary>
public static class GpsOutdoorSceneNames
{
    public const string StandaloneGpsSceneName = "GPSMapPlane";
    public const string HybridGpsMapSceneName = "HybridGPSMap";
    public const string HybridNavigationSceneName = "Hybrid Navigation";
    public const string TestOutdoorBkSceneName = "testOutdoorBK";
    public const string ManSceneName = "ManScene";

    public static readonly IReadOnlyList<string> KnownScenes = new[] { StandaloneGpsSceneName };

    private static readonly HashSet<string> Set = new HashSet<string>(KnownScenes);

    public static bool Includes(string sceneName) => sceneName != null && Set.Contains(sceneName);

    /// <summary>
    /// <see cref="MinimapHeadingIndicator"/> only. Hybrid is excluded from <see cref="Includes"/> so other
    /// RuntimeInitialize hooks (e.g. <c>MobileNavigationHUD</c>) do not double-spawn in HybridGPSMap.
    /// </summary>
    public static bool ShouldAutoSpawnMinimapHeadingIndicator(string sceneName) =>
        Includes(sceneName)
        || string.Equals(sceneName, HybridGpsMapSceneName, System.StringComparison.Ordinal)
        || string.Equals(sceneName, HybridNavigationSceneName, System.StringComparison.Ordinal)
        || string.Equals(sceneName, TestOutdoorBkSceneName, System.StringComparison.OrdinalIgnoreCase)
        || string.Equals(sceneName, ManSceneName, System.StringComparison.Ordinal);
}
