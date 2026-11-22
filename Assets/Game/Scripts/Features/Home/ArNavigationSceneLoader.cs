using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Features.Home
{
    /// <summary>
    /// Lives in the AppShell scene. The first time the AR page opens it loads the B9 navigation scene on top
    /// (additive) and keeps it loaded, so the camera, GPS and VPS keep running while other pages are open.
    /// The shell camera is switched off before the load: the AR camera draws the background, and Camera.main
    /// must resolve to it for the navigation scripts.
    /// </summary>
    public class ArNavigationSceneLoader : MonoBehaviour
    {
        public const string DefaultSceneName = "B9NavigationV2";

        [SerializeField] private string sceneName = DefaultSceneName;
        [SerializeField] private Camera shellCamera;

        private bool requested;

        public string SceneName => sceneName;

        private void OnEnable()
        {
            ArNavigationBridge.VisibilityChanged += OnVisibilityChanged;
            OnVisibilityChanged();
        }

        private void OnDisable()
        {
            ArNavigationBridge.VisibilityChanged -= OnVisibilityChanged;
        }

        private void OnVisibilityChanged()
        {
            if (!Application.isPlaying || requested || !ArNavigationBridge.ArPageVisible)
            {
                return;
            }

            requested = true;
            Scene loaded = SceneManager.GetSceneByName(sceneName);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                SwitchToArCamera(loaded);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"[ArNavigationSceneLoader] {sceneName} is not in the build settings; the AR page has no camera view.");
                return;
            }

            DisableShellCamera();
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[ArNavigationSceneLoader] Could not load {sceneName}.");
                return;
            }

            load.completed += _ => SwitchToArCamera(SceneManager.GetSceneByName(sceneName));
        }

        private void SwitchToArCamera(Scene navigationScene)
        {
            DisableShellCamera();
            if (navigationScene.IsValid() && navigationScene.isLoaded)
            {
                // Objects the navigation scripts create at runtime belong with them, and its lighting applies.
                SceneManager.SetActiveScene(navigationScene);
            }
        }

        private void DisableShellCamera()
        {
            if (shellCamera != null)
            {
                shellCamera.enabled = false;
            }
        }
    }
}
