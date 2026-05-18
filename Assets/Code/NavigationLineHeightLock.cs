using UnityEngine;

/// <summary>
/// Forces every point of a <see cref="LineRenderer"/> to a single world Y. Works for flat prototypes;
/// in AR outdoor prefer <see cref="ARPathFinder"/> with <c>useMeshPath</c> instead — flat Y often looks
/// wrong next to real terrain. Disable this component when using mesh ribbons on the same object.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class NavigationLineHeightLock : MonoBehaviour
{
    [Tooltip("World Y forced for all LineRenderer points. Not recommended with ARPathFinder mesh paths — remove or disable this component.")]
    public float height = -0.11f;
    private LineRenderer lr;

    private Vector3[] positionBuffer = new Vector3[0];

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    // void LateUpdate()
    // {
    //     for (int i = 0; i < lr.positionCount; i++)
    //     {
    //         Vector3 p = lr.GetPosition(i);
    //         p.y = height;
    //         lr.SetPosition(i, p);
    //     }
    // }

    void LateUpdate()
    {
        int count = lr.positionCount;
        if (count == 0) return;

        if (positionBuffer.Length < count)
            positionBuffer = new Vector3[count];

        lr.GetPositions(positionBuffer);
        for (int i = 0; i < count; i++)
            positionBuffer[i].y = height;
        lr.SetPositions(positionBuffer);  // 1 rebuild thay vì N rebuild
    }
}
