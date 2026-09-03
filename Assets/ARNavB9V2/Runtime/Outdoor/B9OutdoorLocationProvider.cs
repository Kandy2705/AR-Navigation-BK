using System.Collections;
using ARNavB9V2.Data;
using UnityEngine;

namespace ARNavB9V2.Outdoor
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class B9OutdoorLocationProvider : MonoBehaviour
    {
        public enum LocationState
        {
            Disabled,
            Initializing,
            Ready,
            PermissionDenied,
            Unavailable,
            TimedOut,
            PoorAccuracy,
        }

        [SerializeField] private B9OutdoorMapDefinition mapDefinition;
        [SerializeField] private float desiredAccuracyMeters = 5f;
        [SerializeField] private float updateDistanceMeters = 1f;
        [SerializeField] private float maximumAcceptedAccuracyMeters = 30f;
        [SerializeField] private float initializationTimeoutSeconds = 20f;
        [Range(0.01f, 1f)] [SerializeField] private float positionSmoothing = 0.2f;
        [SerializeField] private float presentationSmoothTimeSeconds = 0.9f;
        [SerializeField] private float maximumPresentationSpeedMetersPerSecond = 6f;
        [SerializeField] private float snapPresentationDistanceMeters = 35f;
        [SerializeField] private bool useEditorMockLocation = true;
        [SerializeField] private Vector3 editorMockCampusPosition;
        [Range(0f, 360f)] [SerializeField] private float editorMockHeadingDegrees;

        private Coroutine initializationRoutine;
        private bool hasSmoothedPosition;
        private double lastProcessedTimestamp = double.MinValue;
        private float lastSampleReceivedAt = -1f;
        private Vector3 targetCampusPosition;
        private Vector3 presentationVelocity;

        public LocationState State { get; private set; } = LocationState.Disabled;
        public Vector3 CampusPosition { get; private set; }
        public Vector3 TargetCampusPosition => targetCampusPosition;
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public float HorizontalAccuracyMeters { get; private set; } = float.PositiveInfinity;
        public float HeadingDegrees { get; private set; }
        public bool HasHeading { get; private set; }
        public int SampleVersion { get; private set; }
        public bool HasReliableFix => State == LocationState.Ready;
        public float SampleAgeSeconds => lastSampleReceivedAt < 0f
            ? float.PositiveInfinity
            : Mathf.Max(0f, Time.unscaledTime - lastSampleReceivedAt);

        public void Configure(
            B9OutdoorMapDefinition definition,
            bool enableEditorMock,
            Vector3 mockPosition,
            float mockHeading = 0f)
        {
            mapDefinition = definition;
            useEditorMockLocation = enableEditorMock;
            editorMockCampusPosition = mockPosition;
            editorMockHeadingDegrees = mockHeading;
        }

        public void ConfigurePresentationSmoothing(
            float targetBlend,
            float smoothTimeSeconds,
            float maximumSpeedMetersPerSecond)
        {
            positionSmoothing = Mathf.Clamp01(targetBlend);
            presentationSmoothTimeSeconds = Mathf.Max(0.05f, smoothTimeSeconds);
            maximumPresentationSpeedMetersPerSecond = Mathf.Max(0.5f, maximumSpeedMetersPerSecond);
        }

        public void SetSimulatedCampusPosition(Vector3 position, float headingDegrees = 0f)
        {
            useEditorMockLocation = true;
            editorMockCampusPosition = position;
            editorMockHeadingDegrees = Mathf.Repeat(headingDegrees, 360f);
            ApplyMockSample(forceVersionIncrement: true);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (useEditorMockLocation)
            {
                ApplyMockSample(forceVersionIncrement: true);
                return;
            }
#endif
            initializationRoutine = StartCoroutine(InitializeLocation());
        }

        private void OnDisable()
        {
            if (initializationRoutine != null)
            {
                StopCoroutine(initializationRoutine);
                initializationRoutine = null;
            }

            if (Input.location.status == LocationServiceStatus.Running)
                Input.location.Stop();
            Input.compass.enabled = false;
            State = LocationState.Disabled;
        }

        private IEnumerator InitializeLocation()
        {
            if (mapDefinition == null)
            {
                State = LocationState.Unavailable;
                yield break;
            }

            if (!Input.location.isEnabledByUser)
            {
                State = LocationState.PermissionDenied;
                yield break;
            }

            State = LocationState.Initializing;
            Input.compass.enabled = true;
            Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

            float deadline = Time.realtimeSinceStartup + initializationTimeoutSeconds;
            while (Input.location.status == LocationServiceStatus.Initializing
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            initializationRoutine = null;
            if (Input.location.status == LocationServiceStatus.Initializing)
            {
                State = LocationState.TimedOut;
                yield break;
            }

            if (Input.location.status == LocationServiceStatus.Failed)
            {
                State = LocationState.Unavailable;
                yield break;
            }

            SampleDeviceLocation();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (useEditorMockLocation)
            {
                ApplyMockSample(forceVersionIncrement: false);
                return;
            }
#endif
            if (Input.location.status == LocationServiceStatus.Running)
                SampleDeviceLocation();
            AdvancePresentationPosition();
        }

        private void SampleDeviceLocation()
        {
            if (mapDefinition == null)
            {
                State = LocationState.Unavailable;
                return;
            }

            LocationInfo sample = Input.location.lastData;
            if (sample.timestamp <= lastProcessedTimestamp)
                return;

            lastProcessedTimestamp = sample.timestamp;
            lastSampleReceivedAt = Time.unscaledTime;
            Latitude = sample.latitude;
            Longitude = sample.longitude;
            HorizontalAccuracyMeters = sample.horizontalAccuracy;
            HasHeading = Input.compass.enabled;
            HeadingDegrees = HasHeading
                ? Mathf.Repeat(Input.compass.trueHeading, 360f)
                : 0f;

            Vector3 rawPosition = mapDefinition.GpsToCampus(Latitude, Longitude);
            if (!hasSmoothedPosition)
            {
                targetCampusPosition = rawPosition;
                CampusPosition = rawPosition;
                hasSmoothedPosition = true;
            }
            else
            {
                targetCampusPosition = Vector3.Lerp(
                    targetCampusPosition,
                    rawPosition,
                    positionSmoothing);
                if (Vector3.Distance(CampusPosition, targetCampusPosition)
                    >= snapPresentationDistanceMeters)
                {
                    CampusPosition = targetCampusPosition;
                    presentationVelocity = Vector3.zero;
                }
            }

            bool accuracyValid = HorizontalAccuracyMeters > 0f
                                 && HorizontalAccuracyMeters <= maximumAcceptedAccuracyMeters;
            State = accuracyValid ? LocationState.Ready : LocationState.PoorAccuracy;
            SampleVersion++;
        }

        private void AdvancePresentationPosition()
        {
            if (!hasSmoothedPosition)
                return;
            CampusPosition = Vector3.SmoothDamp(
                CampusPosition,
                targetCampusPosition,
                ref presentationVelocity,
                presentationSmoothTimeSeconds,
                maximumPresentationSpeedMetersPerSecond,
                Time.unscaledDeltaTime);
        }

        private void ApplyMockSample(bool forceVersionIncrement)
        {
            if (mapDefinition == null)
            {
                State = LocationState.Unavailable;
                return;
            }

            bool changed = !hasSmoothedPosition
                           || (CampusPosition - editorMockCampusPosition).sqrMagnitude > 0.0001f
                           || Mathf.Abs(Mathf.DeltaAngle(HeadingDegrees, editorMockHeadingDegrees)) > 0.01f;
            targetCampusPosition = editorMockCampusPosition;
            CampusPosition = targetCampusPosition;
            presentationVelocity = Vector3.zero;
            mapDefinition.CampusToGps(CampusPosition, out double latitude, out double longitude);
            Latitude = latitude;
            Longitude = longitude;
            HorizontalAccuracyMeters = 2f;
            HeadingDegrees = Mathf.Repeat(editorMockHeadingDegrees, 360f);
            HasHeading = true;
            State = LocationState.Ready;
            hasSmoothedPosition = true;
            lastSampleReceivedAt = Time.unscaledTime;
            if (changed || forceVersionIncrement)
                SampleVersion++;
        }
    }
}
