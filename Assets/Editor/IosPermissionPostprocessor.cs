#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IosPermissionPostprocessor
{
    [PostProcessBuild(100)]
    public static void AddRequiredPrivacyDescriptions(
        BuildTarget target,
        string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString(
            "NSCameraUsageDescription",
            "Camera is required for AR navigation and indoor visual positioning.");
        plist.root.SetString(
            "NSLocationWhenInUseUsageDescription",
            "Location is required for outdoor navigation and indoor-outdoor handover.");
        File.WriteAllText(plistPath, plist.WriteToString());
    }
}
#endif
