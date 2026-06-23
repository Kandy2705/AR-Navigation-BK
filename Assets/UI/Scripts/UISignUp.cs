using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Legacy UISignUp — class skeleton để Unity resolve script reference trên GameObject "UI Signup".
/// Logic signup cũ đã được thay thế bởi <see cref="WelcomePageController"/> + <see cref="PageFactory"/>.
/// Component này tồn tại để tránh "Missing Script" warning; không chạy code nào.
/// </summary>
[AddComponentMenu("")]
public class UISignUp : MonoBehaviour
{
    private void OnEnable()
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null) return;

        // Legacy — không còn xử lý logic ở đây.
        // Signup flow được điều hướng qua NavigationManager + PageFactory.
    }
}
