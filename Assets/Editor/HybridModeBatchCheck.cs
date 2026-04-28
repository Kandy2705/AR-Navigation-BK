using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HybridModeBatchCheck
{
    private const string ScenePath = "Assets/Scenes/Hybrid Navigation.unity";

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene(ScenePath);

            HybridModeController controller = UnityEngine.Object.FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Fail("HybridModeController not found.");
                return;
            }

            InvokePrivate(controller, "Start");
            AssertARInactive("Initial App Start");

            controller.ApplyInitialMode();
            AssertCurrentMode(controller, "Apply Initial Mode");

            controller.ForceIndoor();
            AssertModeState("Force Indoor", indoorActive: true, outdoorActive: false, expectedMainCamera: "ARCamera");

            controller.ForceOutdoor();
            AssertModeState("Force Outdoor", indoorActive: false, outdoorActive: true, expectedMainCamera: "Main Camera");

            controller.DeactivateARMode();
            AssertARInactive("Deactivate AR");

            Debug.Log("[HybridModeBatchCheck] PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[HybridModeBatchCheck] FAIL: " + ex);
            EditorApplication.Exit(1);
        }
    }

    private static void AssertCurrentMode(HybridModeController controller, string label)
    {
        if (controller.CurrentMode == HybridModeController.HybridMode.Indoor)
        {
            AssertModeState(label, indoorActive: true, outdoorActive: false, expectedMainCamera: "ARCamera");
            return;
        }

        if (controller.CurrentMode == HybridModeController.HybridMode.Outdoor)
        {
            AssertModeState(label, indoorActive: false, outdoorActive: true, expectedMainCamera: "Main Camera");
            return;
        }

        Fail($"{label}: unexpected current mode {controller.CurrentMode}.");
    }

    private static void AssertModeState(string label, bool indoorActive, bool outdoorActive, string expectedMainCamera)
    {
        GameObject indoor = FindSceneObject("IndoorEnvironment");
        GameObject outdoor = FindSceneObject("OutdoorEnvironment");

        if ((indoor != null && indoor.activeInHierarchy) != indoorActive)
        {
            Fail($"{label}: IndoorEnvironment active mismatch.");
        }

        if ((outdoor != null && outdoor.activeInHierarchy) != outdoorActive)
        {
            Fail($"{label}: OutdoorEnvironment active mismatch.");
        }

        AudioListener[] activeListeners = UnityEngine.Object
            .FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(listener => listener.enabled)
            .ToArray();

        if (activeListeners.Length != 1)
        {
            Fail($"{label}: expected exactly 1 active AudioListener, found {activeListeners.Length}.");
        }

        Camera[] mainTaggedCameras = UnityEngine.Object
            .FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(camera => camera.CompareTag("MainCamera"))
            .ToArray();

        if (mainTaggedCameras.Length != 1)
        {
            Fail($"{label}: expected exactly 1 active MainCamera, found {mainTaggedCameras.Length}.");
        }

        if (mainTaggedCameras[0].name != expectedMainCamera)
        {
            Fail($"{label}: expected MainCamera '{expectedMainCamera}', got '{mainTaggedCameras[0].name}'.");
        }

        Debug.Log($"[HybridModeBatchCheck] {label}: OK");
    }

    private static void AssertARInactive(string label)
    {
        GameObject indoor = FindSceneObject("IndoorEnvironment");
        GameObject outdoor = FindSceneObject("OutdoorEnvironment");

        if (indoor != null && indoor.activeInHierarchy)
        {
            Fail($"{label}: IndoorEnvironment should be inactive.");
        }

        if (outdoor != null && outdoor.activeInHierarchy)
        {
            Fail($"{label}: OutdoorEnvironment should be inactive.");
        }

        Debug.Log($"[HybridModeBatchCheck] {label}: OK");
    }

    private static GameObject FindSceneObject(string objectName)
    {
        return Resources
            .FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(gameObject => gameObject.name == objectName &&
                gameObject.scene.IsValid() &&
                !EditorUtility.IsPersistent(gameObject));
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            Fail($"Method not found: {methodName}");
        }

        method.Invoke(target, null);
    }

    private static void Fail(string message)
    {
        throw new InvalidOperationException(message);
    }
}
