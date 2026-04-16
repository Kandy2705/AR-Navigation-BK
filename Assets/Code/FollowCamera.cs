using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform player; // gán Main Camera vào đây

    void Update()
    {
        if (player != null)
        {
            // Vị trí icon luôn trùng X,Z của camera
            Vector3 newPos = player.position;
            newPos.y = transform.position.y; // giữ nguyên độ cao trên minimap
            transform.position = newPos;

            // Xoay icon theo hướng camera (chỉ trục Y)
            Vector3 forward = player.forward;
            forward.y = 0; // bỏ góc ngẩng/cúi
            if (forward.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}
