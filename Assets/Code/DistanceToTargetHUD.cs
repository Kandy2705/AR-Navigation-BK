using TMPro;
using UnityEngine;

public class DistanceToTargetHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform user;
    [SerializeField] private Transform target;
    [SerializeField] private TextMeshProUGUI output;

    [Header("Display")]
    [Tooltip("Measure distance only on XZ plane. In outdoor ENU/map space, 1 Unity unit = 1 meter.")]
    [SerializeField] private bool xzOnly = true;

    [Tooltip("Update interval in seconds (0 = every frame).")]
    [SerializeField] private float updateInterval = 0.1f;

    [SerializeField] private string prefix = "Distance:";

    [Header("Smoothing")]
    [Tooltip("Smooth distance before rounding to whole meters. Lower = more responsive, higher = steadier.")]
    [SerializeField] private float smoothingSeconds = 0.35f;

    private float lastUpdateTime = -999f;
    private float smoothedDistance;
    private bool hasSmoothedDistance;

    public void Configure(Transform userTransform, Transform targetTransform, TextMeshProUGUI outputText)
    {
        user = userTransform;
        target = targetTransform;
        output = outputText;
    }

    private void Update()
    {
        if (output == null || user == null || target == null) return;

        if (updateInterval > 0f && Time.time - lastUpdateTime < updateInterval) return;
        lastUpdateTime = Time.time;

        float d = CalculateDistanceMeters();

        if (!hasSmoothedDistance)
        {
            smoothedDistance = d;
            hasSmoothedDistance = true;
        }
        else
        {
            float t = smoothingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / smoothingSeconds);
            smoothedDistance = Mathf.Lerp(smoothedDistance, d, t);
        }

        int meters = Mathf.Max(0, Mathf.RoundToInt(smoothedDistance));

        output.text = $"{prefix} {meters} m";
    }

    private float CalculateDistanceMeters()
    {
        Vector3 userPosition = user.position;
        Vector3 targetPosition = target.position;

        if (xzOnly)
        {
            userPosition.y = 0f;
            targetPosition.y = 0f;
        }

        return Vector3.Distance(userPosition, targetPosition);
    }
}

