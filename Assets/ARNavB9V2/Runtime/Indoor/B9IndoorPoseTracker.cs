using System;
using ARNavB9V2.Vps;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ARNavB9V2.Indoor
{
    /// <summary>
    /// Fuses AR visual-inertial motion with a lightweight accelerometer step estimate.
    /// It never moves the XR camera; it only supplies a stable, NavMesh-constrained
    /// navigation position to the route and minimap.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class B9IndoorPoseTracker : MonoBehaviour
    {
        [SerializeField] private Camera arCamera;
        [SerializeField] private NavMeshSurface indoorNavMesh;
        [SerializeField] private B9VpsTransitionController vpsTransition;
        [Header("PDR")]
        [SerializeField, Range(0.35f, 1.1f)] private float stepLengthMeters = 0.68f;
        [SerializeField, Range(0.03f, 0.4f)] private float stepTriggerAccelerationG = 0.115f;
        [SerializeField, Range(0.01f, 0.2f)] private float stepReleaseAccelerationG = 0.045f;
        [SerializeField, Range(0.15f, 0.8f)] private float minimumStepIntervalSeconds = 0.28f;
        [SerializeField, Range(0.1f, 2f)] private float visualMotionNeededPerStepMeters = 0.22f;
        [Header("Fusion + map matching")]
        [SerializeField, Range(0.01f, 0.5f)] private float visualRecoveryBlendPerSecond = 0.12f;
        [SerializeField, Range(0.2f, 3f)] private float maximumVisualDeltaPerFrameMeters = 1.2f;
        [SerializeField, Range(0.5f, 6f)] private float navMeshSampleRadiusMeters = 2.5f;
        [SerializeField, Range(0.02f, 0.5f)] private float outputSmoothTimeSeconds = 0.12f;
        [SerializeField, Range(0.5f, 5f)] private float maximumOutputSpeedMetersPerSecond = 2.5f;

        private B9StepDetector stepDetector;
        private Vector3 gravityEstimate;
        private Vector3 rawPosition;
        private Vector3 smoothedPosition;
        private Vector3 smoothVelocity;
        private Vector3 lastCameraPosition;
        private float visualDistanceSinceStep;
        private float lastVisualMotionTime;
        private float lastStepTime;
        private bool sensorInitialized;
        private bool hasPose;

        public bool IsTracking { get; private set; }
        public Vector3 CurrentPosition => hasPose
            ? smoothedPosition
            : arCamera != null ? arCamera.transform.position : Vector3.zero;
        public Vector3 RawPosition => hasPose ? rawPosition : CurrentPosition;
        public float HeadingDegrees { get; private set; }
        public int StepCount { get; private set; }
        public float TravelledDistanceMeters { get; private set; }
        public float MotionStrengthG { get; private set; }
        public float Confidence { get; private set; }
        public bool IsNavMeshConstrained { get; private set; }
        public string SourceLabel { get; private set; } = "Chưa theo dõi";
        public event Action<int, float, Vector3> StepDetected;

        public void Configure(
            Camera displayCamera,
            NavMeshSurface navMeshSurface,
            B9VpsTransitionController transition)
        {
            arCamera = displayCamera;
            indoorNavMesh = navMeshSurface;
            vpsTransition = transition;
            RebuildStepDetector();
        }

        public void ConfigureTuning(
            float stepLength,
            float triggerAcceleration,
            float releaseAcceleration,
            float minimumStepInterval,
            float navMeshRadius)
        {
            stepLengthMeters = Mathf.Clamp(stepLength, 0.35f, 1.1f);
            stepTriggerAccelerationG = Mathf.Clamp(triggerAcceleration, 0.03f, 0.4f);
            stepReleaseAccelerationG = Mathf.Clamp(
                releaseAcceleration,
                0.01f,
                stepTriggerAccelerationG * 0.9f);
            minimumStepIntervalSeconds = Mathf.Clamp(minimumStepInterval, 0.15f, 0.8f);
            navMeshSampleRadiusMeters = Mathf.Clamp(navMeshRadius, 0.5f, 6f);
            RebuildStepDetector();
        }

        private void Awake()
        {
            RebuildStepDetector();
            enabled = false;
        }

        public void BeginTracking()
        {
            if (arCamera == null)
                return;

            RebuildStepDetector();
            stepDetector.Reset();
            Input.gyro.enabled = true;
            Input.compass.enabled = true;
            gravityEstimate = Input.acceleration;
            lastCameraPosition = arCamera.transform.position;
            rawPosition = ConstrainToNavMesh(lastCameraPosition, out bool constrained);
            smoothedPosition = rawPosition;
            smoothVelocity = Vector3.zero;
            visualDistanceSinceStep = 0f;
            lastVisualMotionTime = Time.unscaledTime;
            lastStepTime = float.NegativeInfinity;
            StepCount = 0;
            TravelledDistanceMeters = 0f;
            MotionStrengthG = 0f;
            Confidence = constrained ? 0.9f : 0.72f;
            IsNavMeshConstrained = constrained;
            SourceLabel = constrained ? "AR + NavMesh" : "AR/VIO";
            sensorInitialized = true;
            hasPose = true;
            IsTracking = true;
            enabled = true;
        }

        public void StopTracking()
        {
            IsTracking = false;
            sensorInitialized = false;
            SourceLabel = "Chưa theo dõi";
            enabled = false;
        }

        private void Update()
        {
            if (!IsTracking || arCamera == null)
                return;
            if (vpsTransition != null
                && vpsTransition.State != B9VpsTransitionController.TransitionState.IndoorLocalized)
                return;

            UpdateHeading();
            ApplyVisualInertialMotion();
            DetectAndApplyStep();
            rawPosition = ConstrainToNavMesh(rawPosition, out bool constrained);
            IsNavMeshConstrained = constrained;
            smoothedPosition = Vector3.SmoothDamp(
                smoothedPosition,
                rawPosition,
                ref smoothVelocity,
                outputSmoothTimeSeconds,
                maximumOutputSpeedMetersPerSecond,
                Time.unscaledDeltaTime);
            UpdateConfidenceAndSource(constrained);
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

        private void ApplyVisualInertialMotion()
        {
            Vector3 cameraPosition = arCamera.transform.position;
            Vector3 delta = cameraPosition - lastCameraPosition;
            lastCameraPosition = cameraPosition;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= 0.001f || distance > maximumVisualDeltaPerFrameMeters)
                return;

            rawPosition += delta;
            visualDistanceSinceStep += distance;
            lastVisualMotionTime = Time.unscaledTime;

            // If PDR carried the estimate while visual tracking was weak, gently pull
            // it back when AR movement becomes available again.
            Vector3 cameraGround = new Vector3(cameraPosition.x, rawPosition.y, cameraPosition.z);
            rawPosition = Vector3.Lerp(
                rawPosition,
                cameraGround,
                visualRecoveryBlendPerSecond * Time.unscaledDeltaTime);
        }

        private void DetectAndApplyStep()
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
            lastStepTime = Time.unscaledTime;
            TravelledDistanceMeters += stepLengthMeters;

            // ARKit usually already contains the physical step. Only add the missing
            // part when its visual displacement was too small, preventing double travel.
            float missingDistance = Mathf.Clamp(
                stepLengthMeters - visualDistanceSinceStep,
                0f,
                stepLengthMeters);
            if (visualDistanceSinceStep < visualMotionNeededPerStepMeters
                && missingDistance > 0.02f)
            {
                Vector3 heading = Quaternion.Euler(0f, HeadingDegrees, 0f) * Vector3.forward;
                rawPosition += heading * missingDistance;
            }

            visualDistanceSinceStep = 0f;
            StepDetected?.Invoke(StepCount, Time.unscaledTime, rawPosition);
        }

        private Vector3 ConstrainToNavMesh(Vector3 position, out bool constrained)
        {
            constrained = false;
            if (indoorNavMesh == null || indoorNavMesh.navMeshData == null)
                return position;
            if (!NavMesh.SamplePosition(
                    position,
                    out NavMeshHit hit,
                    navMeshSampleRadiusMeters,
                    NavMesh.AllAreas))
                return position;

            constrained = true;
            return hit.position;
        }

        private void UpdateConfidenceAndSource(bool constrained)
        {
            bool visualRecent = Time.unscaledTime - lastVisualMotionTime <= 1.5f;
            bool stepRecent = Time.unscaledTime - lastStepTime <= 1.2f;
            if (visualRecent)
            {
                Confidence = constrained ? 0.94f : 0.82f;
                SourceLabel = constrained ? "AR + bước + NavMesh" : "AR + bước";
            }
            else if (stepRecent)
            {
                Confidence = constrained ? 0.78f : 0.62f;
                SourceLabel = constrained ? "Bước chân + NavMesh" : "Bước chân";
            }
            else
            {
                Confidence = constrained ? 0.82f : 0.65f;
                SourceLabel = constrained ? "Giữ vị trí trên NavMesh" : "Giữ vị trí";
            }
        }

        private void RebuildStepDetector()
        {
            stepDetector = new B9StepDetector(
                stepTriggerAccelerationG,
                stepReleaseAccelerationG,
                minimumStepIntervalSeconds);
        }
    }
}
