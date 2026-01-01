using UnityEngine;
using UnityEngine.UIElements;

public class UIWelcome : MonoBehaviour
{
    [SerializeField] private UIRouter router;

    private Button btnLogin;
    private Button btnSignup;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null) return;

        btnLogin = root.Q<Button>("LoginButton");
        btnSignup = root.Q<Button>("SignUpButton");

        btnLogin?.SetEnabled(true);
        btnSignup?.SetEnabled(true);

        if (btnLogin != null)
        {
            btnLogin.clicked -= OnLoginClicked;
            btnLogin.clicked += OnLoginClicked;
        }

        if (btnSignup != null)
        {
            btnSignup.clicked -= OnSignupClicked;
            btnSignup.clicked += OnSignupClicked;
        }
    }

    private void OnDisable()
    {
        if (btnLogin != null) btnLogin.clicked -= OnLoginClicked;
        if (btnSignup != null) btnSignup.clicked -= OnSignupClicked;
    }

    private void OnLoginClicked()
    {
        btnLogin?.SetEnabled(false);
        router.ShowLogin();
    }

    private void OnSignupClicked()
    {
        btnSignup?.SetEnabled(false);
        router.ShowSignUp();
    }
}
