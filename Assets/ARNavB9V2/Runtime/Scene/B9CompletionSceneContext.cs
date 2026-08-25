using ARNavB9V2.Experiment;
using ARNavB9V2.Indoor;
using UnityEngine;

namespace ARNavB9V2.Scene
{
    [DisallowMultipleComponent]
    public sealed class B9CompletionSceneContext : MonoBehaviour
    {
        [SerializeField] private B9IndoorPoseTracker indoorPoseTracker;
        [SerializeField] private B9ExperimentLogger experimentLogger;
        [SerializeField] private B9IosRuntimeGuard iosRuntimeGuard;

        public B9IndoorPoseTracker IndoorPoseTracker => indoorPoseTracker;
        public B9ExperimentLogger ExperimentLogger => experimentLogger;
        public B9IosRuntimeGuard IosRuntimeGuard => iosRuntimeGuard;

        public void Configure(
            B9IndoorPoseTracker poseTracker,
            B9ExperimentLogger logger,
            B9IosRuntimeGuard runtimeGuard)
        {
            indoorPoseTracker = poseTracker;
            experimentLogger = logger;
            iosRuntimeGuard = runtimeGuard;
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (indoorPoseTracker == null)
                return Fail("Indoor AR/PDR pose tracker missing", out reason);
            if (experimentLogger == null)
                return Fail("Research experiment logger missing", out reason);
            if (iosRuntimeGuard == null)
                return Fail("iOS runtime guard missing", out reason);
            reason = string.Empty;
            return true;
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
