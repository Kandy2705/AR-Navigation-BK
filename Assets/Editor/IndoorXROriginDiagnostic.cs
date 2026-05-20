#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dump cấu hình XR Origin / ARCamera của Indoor stack (bao gồm node inactive).
/// Hữu ích khi IndoorEnvironment chưa được kích hoạt — MCP get_gameobject không
/// tìm thấy nhưng utility này vẫn quét được qua Resources.FindObjectsOfTypeAll.
///
/// Menu: Tools/Indoor/Diagnose XR Origin
/// </summary>
public static class IndoorXROriginDiagnostic
{
    [MenuItem("Tools/Indoor/Diagnose XR Origin")]
    public static void Run()
    {
        string report = "=== XR ORIGIN DIAGNOSTIC ===\n\n";

        // Tìm mọi XR Origin trong scene (bao gồm inactive).
        var allXrOrigins = Resources.FindObjectsOfTypeAll<Unity.XR.CoreUtils.XROrigin>()
            .Where(o => o != null && o.gameObject.scene.IsValid())
            .ToArray();

        report += $"Total XR Origins in scene: {allXrOrigins.Length}\n\n";

        for (int i = 0; i < allXrOrigins.Length; i++)
        {
            var xr = allXrOrigins[i];
            string fullPath = GetPath(xr.transform);
            report += $"--- XR Origin #{i} ---\n";
            report += $"  Path: {fullPath}\n";
            report += $"  Active (self): {xr.gameObject.activeSelf}, Active (hierarchy): {xr.gameObject.activeInHierarchy}\n";
            report += $"  Position: {xr.transform.position}, Rotation: {xr.transform.eulerAngles}\n";
            report += $"  Scale: {xr.transform.lossyScale}\n";

            // XROrigin properties
            report += $"  TrackingOriginMode: {xr.RequestedTrackingOriginMode}\n";
            report += $"  CameraYOffset: {xr.CameraYOffset:F3}\n";
            report += $"  CurrentTrackingOriginMode: {xr.CurrentTrackingOriginMode}\n";

            // CameraFloorOffset child
            if (xr.CameraFloorOffsetObject != null)
            {
                report += $"  CameraFloorOffsetObject: '{xr.CameraFloorOffsetObject.name}'" +
                          $" pos={xr.CameraFloorOffsetObject.transform.localPosition}\n";
            }
            else
            {
                report += $"  CameraFloorOffsetObject: NULL\n";
            }

            // Camera
            var cam = xr.Camera;
            if (cam != null)
            {
                report += $"  Camera: '{cam.name}' tag={cam.gameObject.tag}\n";
                report += $"    localPos={cam.transform.localPosition}, worldPos={cam.transform.position}\n";
                report += $"    enabled={cam.enabled}, fov={cam.fieldOfView}\n";
                var poseDriver = cam.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                if (poseDriver != null)
                {
                    report += $"    TrackedPoseDriver: trackingType={poseDriver.trackingType}, updateType={poseDriver.updateType}\n";
                }
                var arCam = cam.GetComponent<UnityEngine.XR.ARFoundation.ARCameraManager>();
                report += $"    ARCameraManager: {(arCam != null ? $"present (enabled={arCam.enabled})" : "MISSING")}\n";
                var arBg = cam.GetComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
                report += $"    ARCameraBackground: {(arBg != null ? $"present (enabled={arBg.enabled})" : "MISSING")}\n";
            }
            else
            {
                report += $"  Camera: NULL!\n";
            }
            report += "\n";
        }

        // AR Sessions
        var sessions = Resources.FindObjectsOfTypeAll<UnityEngine.XR.ARFoundation.ARSession>()
            .Where(s => s != null && s.gameObject.scene.IsValid())
            .ToArray();
        report += $"Total AR Sessions: {sessions.Length}\n";
        foreach (var s in sessions)
        {
            report += $"  - '{s.gameObject.name}' active={s.gameObject.activeSelf}/{s.gameObject.activeInHierarchy} path={GetPath(s.transform)}\n";
        }
        report += "\n";

        // MainCamera tag holders
        var mainCams = Resources.FindObjectsOfTypeAll<Camera>()
            .Where(c => c != null && c.gameObject.scene.IsValid() && c.CompareTag("MainCamera"))
            .ToArray();
        report += $"Cameras tagged 'MainCamera': {mainCams.Length}\n";
        foreach (var c in mainCams)
        {
            report += $"  - '{c.name}' active={c.gameObject.activeSelf}/{c.gameObject.activeInHierarchy} path={GetPath(c.transform)}\n";
        }

        report += "\n=== END ===";
        Debug.Log(report);
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "<null>";
        var sb = new System.Text.StringBuilder(t.name);
        var cur = t.parent;
        while (cur != null)
        {
            sb.Insert(0, "/");
            sb.Insert(0, cur.name);
            cur = cur.parent;
        }
        return sb.ToString();
    }
}
#endif
