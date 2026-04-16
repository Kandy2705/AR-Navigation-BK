using UnityEngine;

namespace ImportedSceneScripts
{
    public class GlobalProperties : MonoBehaviour
    {
        public static GlobalProperties Instance;
        public bool IsShowNavigation = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }
}
