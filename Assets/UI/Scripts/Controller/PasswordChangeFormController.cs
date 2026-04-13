using UnityEngine;
using UnityEngine.UIElements;

public class PasswordChangeFormController : IPageController
{    
    private TextField _passwordInput;
    private TextField _confirmPasswordInput;
    private Button _btnSubmit;
    private bool isPasswordVisible = false;
    private bool isConfirmPasswordVisible = false;
    private VisualElement ToggleEyeIcon;
    private VisualElement ConfirmToggleEyeIcon;
    private NavigationManager navigationManager;

    public void Start(VisualElement root)
    {
        _passwordInput = root.Q<TextField>("PasswordInput");
        _confirmPasswordInput = root.Q<TextField>("ConfirmPasswordInput");
        _btnSubmit = root.Q<Button>("btn-submit");
        ToggleEyeIcon = root.Q<VisualElement>("ToggleEyeIcon");
        ConfirmToggleEyeIcon = root.Q<VisualElement>("ConfirmToggleEyeIcon");

        if (ToggleEyeIcon != null)
        {
            ToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _passwordInput, ToggleEyeIcon, ref isPasswordVisible));
        }
        if(ConfirmToggleEyeIcon != null)
        {
            ConfirmToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _confirmPasswordInput, ConfirmToggleEyeIcon, ref isConfirmPasswordVisible));
        }
    }
    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        Start(root);
        navigator.BindButton(root, "Btn-Back", PageID.OTPPage, true);
        navigator.BindButton(root, "btn-submit", PageID.Login, false);
    }

    
}