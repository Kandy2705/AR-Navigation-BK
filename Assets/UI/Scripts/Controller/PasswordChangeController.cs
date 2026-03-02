using UnityEngine;
using UnityEngine.UIElements;

public class PasswordChangeController : IPageController
{
    private TextField _oldpasswordInput;
    private TextField _newpasswordInput;
    private TextField _confirmNewPasswordInput;
    private Button _btnSubmit;
    private bool isOldPasswordVisible = false;
    private bool isNewPasswordVisible = false;
    private bool isConfirmNewPasswordVisible = false;
    private VisualElement oldToggleEyeIcon;
    private VisualElement newToggleEyeIcon;
    private VisualElement confirmToggleEyeIcon;
    private NavigationManager navigationManager;
    private void Start(VisualElement root)
    {
        _oldpasswordInput = root.Q<TextField>("OldPasswordInput");
        _newpasswordInput = root.Q<TextField>("NewPasswordInput");
        _confirmNewPasswordInput = root.Q<TextField>("ConfirmNewPasswordInput");

        _btnSubmit = root.Q<Button>("btn-submit");

        oldToggleEyeIcon = root.Q<VisualElement>("OldToggleEyeIcon");
        newToggleEyeIcon = root.Q<VisualElement>("NewToggleEyeIcon");
        confirmToggleEyeIcon = root.Q<VisualElement>("ConfirmToggleEyeIcon");

        if (oldToggleEyeIcon != null)
        {
            oldToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _oldpasswordInput, oldToggleEyeIcon, ref isOldPasswordVisible));
        }
        if(newToggleEyeIcon != null)
        {
            newToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _newpasswordInput, newToggleEyeIcon, ref isNewPasswordVisible));
        }
        if(confirmToggleEyeIcon != null)
        {
            confirmToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _confirmNewPasswordInput, confirmToggleEyeIcon, ref isConfirmNewPasswordVisible));
        }
    }

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigationManager = navigator;

        Start(root);
        navigator.BindButton(root, "Btn-Back", navigator.PreviousPage(), true);
        navigator.BindButton(root, "Btn-Confirm", PageID.OTPPage, false);
        //navigator.ShowPasswordButton(root);
    }
}