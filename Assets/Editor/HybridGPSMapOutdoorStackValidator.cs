#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// HybridGPSMap.unity is often serialised as binary — use this menu to inspect outdoor HUD / dropdown wiring.
/// </summary>
public static class HybridGPSMapOutdoorStackValidator
{
    private const string HybridScenePath = "Assets/Scenes/HybridGPSMap.unity";

    [MenuItem("Tools/TestAR/HybridGPSMap/Validate Outdoor Stack (HybridGPSMap)")]
    public static void Validate()
    {
        Scene scene = EditorSceneManager.OpenScene(HybridScenePath, OpenSceneMode.Single);
        Debug.Log($"[HybridGPSMapValidator] Scene: {scene.path} ({scene.name})");

        GameObject outdoor = GameObject.Find("OutdoorNavigationUI");
        if (outdoor == null)
        {
            Debug.LogError("[HybridGPSMapValidator] Missing root 'OutdoorNavigationUI'. Run Tools/TestAR/HybridGPSMap/Setup App Shell + Outdoor Navigation Hierarchy.");
            return;
        }

        Debug.Log($"[HybridGPSMapValidator] OutdoorNavigationUI activeSelf={outdoor.activeSelf} (expected false in edit mode until Outdoor).");

        var hud = outdoor.GetComponentInChildren<MobileNavigationHUD>(true);
        if (hud == null)
        {
            Debug.LogError("[HybridGPSMapValidator] No MobileNavigationHUD under OutdoorNavigationUI.");
            return;
        }

        var dd = hud.targetDropdown;
        if (dd == null)
        {
            Debug.LogError("[HybridGPSMapValidator] MobileNavigationHUD.targetDropdown is null.");
            return;
        }

        Debug.Log($"[HybridGPSMapValidator] Dropdown GO='{dd.gameObject.name}' template={(dd.template != null ? dd.template.name : "NULL")} options={dd.options?.Count ?? 0}");

        if (dd.template == null)
        {
            Debug.LogError("[HybridGPSMapValidator] Dropdown.template is unassigned — opening the list will error.");
            return;
        }

        var tmplCg = dd.template.GetComponent<CanvasGroup>();
        var tmplCanvas = dd.template.GetComponent<Canvas>();
        Debug.Log($"[HybridGPSMapValidator] Template: Canvas={(tmplCanvas != null)} CanvasGroup={(tmplCg != null)} (CanvasGroup expected after first Show; safe if added at build time).");

        var toggle = dd.template.GetComponentInChildren<Toggle>(true);
        if (toggle == null)
            Debug.LogError("[HybridGPSMapValidator] Template has no Toggle child — invalid Unity dropdown structure.");

        GameObject mask = GameObject.Find("Minimap Circle Mask");
        GameObject minimap = GameObject.Find("Minimap");
        Debug.Log($"[HybridGPSMapValidator] MinimapHeadingIndicator host: CircleMask={(mask != null)} RawImageMinimap={(minimap != null && minimap.GetComponent<RawImage>() != null)}");
    }
}
#endif
