#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes <see cref="NavigationPathMaterialHelper.CreateChevronStripTexture"/> to Assets so it can be assigned on ARPathFinder.
/// </summary>
public static class NavPathChevronBaker
{
    private const string DefaultPath = "Assets/Textures/NavPathChevron.png";

    [MenuItem("Tools/TestAR/Generate NavPath Chevron Texture")]
    public static void Bake()
    {
        string dir = Path.GetDirectoryName(DefaultPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tex = NavigationPathMaterialHelper.CreateChevronStripTexture();
        File.WriteAllBytes(DefaultPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(DefaultPath, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[NavPathChevronBaker] Wrote {DefaultPath}. Assign as Path Chevron Texture on ARPathFinder if desired.");
    }
}
#endif
