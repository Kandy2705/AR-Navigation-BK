using UnityEngine;
using UnityEngine.UIElements;
using Proyecto26;

public class PasswordConfirmController : IPageController
{
    private Label ErrorText;
    private TextField _passwordInput;
    private TextField _confirmPasswordInput;
    private VisualElement ToggleEyeIcon;
    private VisualElement ConfirmToggleEyeIcon;
    private Button _btnSubmit;
    private bool isPasswordVisible = false;
    private bool isConfirmPasswordVisible = false;
    private NavigationManager navigationManager;
    private const string BASE_API = AppConst.BASE_API + "/users/create-customer";

    public void Initialize(VisualElement root, NavigationManager navigator)
    {
        navigationManager = navigator;
        Start(root);
        navigator.BindButton(root, "Btn-Back", navigator.PreviousPage(), true);
        //navigator.ShowPasswordButton(root);
    }

    public void Start(VisualElement root)
    {
        _passwordInput = root.Q<TextField>("PasswordInput");
        _confirmPasswordInput = root.Q<TextField>("ConfirmPasswordInput");
        ErrorText = root.Q<Label>("ErrorLabel");

        ToggleEyeIcon = root.Q<VisualElement>("ToggleEyeIcon");
        ConfirmToggleEyeIcon = root.Q<VisualElement>("ConfirmToggleEyeIcon");
        _btnSubmit = root.Q<Button>("Btn-Confirm");

        if (ToggleEyeIcon != null)
        {
            ToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _passwordInput, ToggleEyeIcon, ref isPasswordVisible));
        }
        if(ConfirmToggleEyeIcon != null)
        {
            ConfirmToggleEyeIcon.RegisterCallback<ClickEvent>(evt => navigationManager.OnTogglePasswordClick(evt, _confirmPasswordInput, ConfirmToggleEyeIcon, ref isConfirmPasswordVisible));
        }
        if(_btnSubmit != null)
        {
            _btnSubmit.clicked += HandleConfirmRegister;
        }
    }

    private void HandleConfirmRegister()
    {
        var myData = RegisterPageController.CurrentData;

        myData.password = _passwordInput.value;

        RestClient.Post(BASE_API, myData)
        .Then(response => 
        {
            navigationManager.Navigate(PageID.Login, false);
            Debug.Log("Thành công rồi! Server trả về: " + response.Text);
        })
        .Catch(error => 
        {
            Debug.LogError("Lỗi: " + error.Message);
        });
    }
}