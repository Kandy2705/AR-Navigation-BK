using UnityEngine;

namespace ARNavB9V2.Indoor
{
    /// <summary>Small deterministic peak detector used by the indoor PDR tracker.</summary>
    public sealed class B9StepDetector
    {
        private readonly float triggerThreshold;
        private readonly float releaseThreshold;
        private readonly float minimumIntervalSeconds;
        private bool peakArmed = true;
        private float lastStepTime = float.NegativeInfinity;

        public B9StepDetector(
            float trigger = 0.115f,
            float release = 0.045f,
            float minimumInterval = 0.28f)
        {
            triggerThreshold = Mathf.Max(0.01f, trigger);
            releaseThreshold = Mathf.Clamp(release, 0.005f, triggerThreshold * 0.9f);
            minimumIntervalSeconds = Mathf.Max(0.15f, minimumInterval);
        }

        public bool Process(float dynamicAccelerationG, float timestamp)
        {
            if (!peakArmed)
            {
                if (dynamicAccelerationG <= releaseThreshold)
                    peakArmed = true;
                return false;
            }

            if (dynamicAccelerationG < triggerThreshold
                || timestamp - lastStepTime < minimumIntervalSeconds)
                return false;

            peakArmed = false;
            lastStepTime = timestamp;
            return true;
        }

        public void Reset()
        {
            peakArmed = true;
            lastStepTime = float.NegativeInfinity;
        }
    }
}
