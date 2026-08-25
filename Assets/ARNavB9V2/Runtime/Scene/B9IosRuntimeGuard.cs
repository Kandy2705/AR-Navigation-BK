using UnityEngine;

namespace ARNavB9V2.Scene
{
    /// <summary>Applies the mobile runtime policy required by continuous AR navigation.</summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class B9IosRuntimeGuard : MonoBehaviour
    {
        [SerializeField, Range(30, 120)] private int targetFrameRate = 60;
        [SerializeField] private bool keepScreenAwake = true;
        [SerializeField] private bool lockPortrait = true;

        public int TargetFrameRate => targetFrameRate;
        public bool KeepsScreenAwake => keepScreenAwake;
        public bool LocksPortrait => lockPortrait;

        private void Awake()
        {
            ApplyPolicy();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ApplyPolicy();
        }

        private void ApplyPolicy()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            if (keepScreenAwake)
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (lockPortrait)
                Screen.orientation = ScreenOrientation.Portrait;
        }
    }
}
