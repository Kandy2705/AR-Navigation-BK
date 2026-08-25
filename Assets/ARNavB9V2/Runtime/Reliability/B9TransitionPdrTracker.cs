using System;
using ARNavB9V2.Indoor;
using UnityEngine;

namespace ARNavB9V2.Reliability
{
    /// <summary>
    /// Short-range dead reckoning used only between the outer B9 handover volume
    /// and a successful MultiSet pose. AR/VIO supplies normal movement while
    /// accelerometer steps fill gaps when visual motion is weak.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    [DisallowMultipleComponent]
    public sealed class B9TransitionPdrTracker : MonoBehaviour
    {
        [SerializeField] private Camera arCamera;
        [SerializeField, Range(0.35f, 1.1f)] private float stepLengthMeters = 0.68f;
        [SerializeField, Range(0.03f, 0.4f)] private float stepTriggerAccelerationG = 0.115f;
        [SerializeField, Range(0.01f, 0.2f)] private float stepReleaseAccelerationG = 0.045f;
        [SerializeField, Range(0.15f, 0.8f)] private float minimumStepIntervalSeconds = 0.28f;
        [SerializeField, Range(0.05f, 0.5f)] private float visualMotionNeededPerStepMeters = 0.22f;
        [SerializeField, Range(0.3f, 3f)] private float maximumVisualDeltaPerFrameMeters = 1.2f;
        [SerializeField, Range(0.02f, 0.5f)] private float outputSmoothTimeSeconds = 0.1f;
        [SerializeField, Range(0.5f, 5f)] private float maximumOutputSpeedMetersPerSecond = 2.8f;

        private B9StepDetector stepDetector;
        private Vector3 gravityEstimate;
        private Vector3 rawCampusPosition;
        private Vector3 smoothCampusPosition;
        private Vector3 smoothVelocity;
        private Vector3 lastCameraPosition;
        private float visualDistanceSinceStep;
        private float lastVisualMotionAt;
        private float lastStepAt;
        private bool sensorInitialized;

        public bool IsTracking { get; private set; }
        public Vector3 CampusPosition => smoothCampusPosition;
        public Vector3 RawCampusPosition => rawCampusPosition;
        public float HeadingDegrees { get; private set; }
        public int StepCount { get; private set; }
        public float TravelledDistanceMeters { get; private set; }
        public float MotionStrengthG { get; private set; }
        public float Confidence { get; private set; }
        public event Action<int, Vector3> StepDetected;

        public void Configure(Camera displayCamera)
        {
            arCamera = displayCamera;
            RebuildDetector();
        }

        private void Awake()
        {
            RebuildDetector();
            enabled = false;
        }

        public void BeginTracking(Vector3 seedCampusPosition)
        {
            if (arCamera == null)
                return;

            RebuildDetector();
            stepDetector.Reset();
            Input.gyro.enabled = true;
            Input.compass.enabled = true;
            gravityEstimate = Input.acceleration;
            lastCameraPosition = arCamera.transform.position;
            rawCampusPosition = seedCampusPosition;
            smoothCampusPosition = seedCampusPosition;
            smoothVelocity = Vector3.zero;
            visualDistanceSinceStep = 0f;
            lastVisualMotionAt = Time.unscaledTime;
            lastStepAt = float.NegativeInfinity;
            StepCount = 0;
            TravelledDistanceMeters = 0f;
            Confidence = 0.82f;
            sensorInitialized = true;
            IsTracking = true;
            enabled = true;
            UpdateHeading();
        }

        public void StopTracking()
        {
            IsTracking = false;
            enabled = false;
        }

        private void Update()
        {
            if (!IsTracking || arCamera == null)
                return;

            UpdateHeading();
            ApplyVisualMotion();
            DetectStep();
            smoothCampusPosition = Vector3.SmoothDamp(
                smoothCampusPosition,
                rawCampusPosition,
                ref smoothVelocity,
                outputSmoothTimeSeconds,
                maximumOutputSpeedMetersPerSecond,
                Time.unscaledDeltaTime);

            bool visualRecent = Time.unscaledTime - lastVisualMotionAt <= 1.5f;
            bool stepRecent = Time.unscaledTime - lastStepAt <= 1.2f;
            Confidence = visualRecent ? 0.9f : stepRecent ? 0.74f : 0.58f;
        }

        private void UpdateHeading()
        {
            Vector3 forward = arCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return;
            forward.Normalize();
            HeadingDegrees = Mathf.Repeat(
                Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
                360f);
        }

        private void ApplyVisualMotion()
        {
            Vector3 cameraPosition = arCamera.transform.position;
            Vector3 delta = cameraPosition - lastCameraPosition;
            lastCameraPosition = cameraPosition;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= 0.001f || distance > maximumVisualDeltaPerFrameMeters)
                return;

            rawCampusPosition += delta;
            visualDistanceSinceStep += distance;
            lastVisualMotionAt = Time.unscaledTime;
        }

        private void DetectStep()
        {
            Vector3 acceleration = Input.acceleration;
            if (!sensorInitialized)
            {
                gravityEstimate = acceleration;
                sensorInitialized = true;
            }

            float gravityBlend = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
            gravityEstimate = Vector3.Lerp(gravityEstimate, acceleration, gravityBlend);
            MotionStrengthG = (acceleration - gravityEstimate).magnitude;
            if (!stepDetector.Process(MotionStrengthG, Time.unscaledTime))
                return;

            StepCount++;
            lastStepAt = Time.unscaledTime;
            TravelledDistanceMeters += stepLengthMeters;
            float missingDistance = Mathf.Clamp(
                stepLengthMeters - visualDistanceSinceStep,
                0f,
                stepLengthMeters);
            if (visualDistanceSinceStep < visualMotionNeededPerStepMeters
                && missingDistance > 0.02f)
            {
                Vector3 heading = Quaternion.Euler(0f, HeadingDegrees, 0f) * Vector3.forward;
                rawCampusPosition += heading * missingDistance;
            }

            visualDistanceSinceStep = 0f;
            StepDetected?.Invoke(StepCount, smoothCampusPosition);
        }

        private void RebuildDetector()
        {
            stepDetector = new B9StepDetector(
                stepTriggerAccelerationG,
                stepReleaseAccelerationG,
                minimumStepIntervalSeconds);
        }
    }
}
