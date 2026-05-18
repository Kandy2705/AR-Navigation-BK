using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hides the decorative XR Origin "Capsule" body mesh in outdoor GPS scenes so user facing is shown
/// only on the minimap (<see cref="MinimapHeadingIndicator"/>), not as a 3D icon in the world.
/// </summary>
public static class OutdoorXrCapsuleMeshHider
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HideCapsuleMeshInOutdoorScenes()
    {
        string name = SceneManager.GetActiveScene().name;
        if (name != GpsOutdoorSceneNames.StandaloneGpsSceneName &&
            name != GpsOutdoorSceneNames.HybridGpsMapSceneName &&
            name != GpsOutdoorSceneNames.HybridNavigationSceneName &&
            name != GpsOutdoorSceneNames.ManSceneName)
        {
            return;
        }

        foreach (var xr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (xr.name != "XR Origin") continue;
            var capT = xr.Find("Capsule");
            if (capT == null) continue;
            var mr = capT.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.enabled = false;
        }
    }
}
