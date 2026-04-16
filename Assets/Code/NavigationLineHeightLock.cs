using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class NavigationLineHeightLock : MonoBehaviour
{
    public float height = 0.89f;
    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
    }

    void LateUpdate()
    {
        for (int i = 0; i < lr.positionCount; i++)
        {
            Vector3 p = lr.GetPosition(i);
            p.y = height;
            lr.SetPosition(i, p);
        }
    }
}
