using UnityEngine;

public class UIRouter : MonoBehaviour
{
    /// <summary>
    /// Fired once when the user reaches the home page (onboarding complete).
    /// HybridOutdoorNavigationRoot listens to this to defer outdoor nav activation.
    /// </summary>
    public static event System.Action OnHomePageShown;

    [Header("UIDoc GameObjects")]
    [SerializeField] private GameObject uiOnboarding;
    [SerializeField] private GameObject uiWelcome;
    [SerializeField] private GameObject uiLogin;
    [SerializeField] private GameObject uiSignUp;
    [SerializeField] private GameObject uiHomePage;

    private void Awake()
    {
        // Optional: ensure initial state
        ShowOnboarding();
    }

    public void ShowOnboarding()
    {
        SetOnly(uiOnboarding);
    }

    public void ShowWelcome()
    {
        SetOnly(uiWelcome);
    }

    public void ShowLogin()
    {
        SetOnly(uiLogin);
    }

    public void ShowSignUp()
    {
        SetOnly(uiSignUp);
    }

    public void ShowHomePage()
    {
        Debug.Log("vao home page duoc roi ne");
        SetOnly(uiHomePage);
        OnHomePageShown?.Invoke();
    }

    private void SetOnly(GameObject active)
    {
        if (uiOnboarding != null) uiOnboarding.SetActive(uiOnboarding == active);
        if (uiWelcome != null) uiWelcome.SetActive(uiWelcome == active);
        if (uiLogin != null) uiLogin.SetActive(uiLogin == active);
        if (uiSignUp != null) uiSignUp.SetActive(uiSignUp == active);
        if (uiHomePage != null) uiHomePage.SetActive(uiHomePage == active);
    }
}
