using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class UIOnboarding : MonoBehaviour
{
    private VisualElement onboardingElement;
    private VisualElement bgElement;
    private VisualElement LoadingScreenElement;
    private VisualElement loadingSpinner;
    public GameObject routerManager;

    private float spinnerAngle = 0f;
    private float loadingDuration = 1.8f;
    private float loadingTimer = 0f;

    private Button btnStart;
    [SerializeField] private UIRouter router;

    void Start()
    {
        var root = GetComponent<UIDocument>()?.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("UIDocument or rootVisualElement not found.");
            return;
        }

        CaretStyleApplier.Apply(root);

        onboardingElement = root.Q<VisualElement>("Onboarding");
        bgElement = root.Q<VisualElement>("Background");
        loadingSpinner = root.Q<VisualElement>("LoadingSpinner");
        LoadingScreenElement = root.Q<VisualElement>("LoadingScreen");

        btnStart = root.Q<Button>("NextOnboardingButton");

        //onboardingElement?.EnableInClassList("screen-visible", true);
        //onboardingElement?.EnableInClassList("screen-hidden", false);

        //welcomeElement?.EnableInClassList("screen-visible", false);
        //welcomeElement?.EnableInClassList("screen-hidden", true);
        onboardingElement.style.display = DisplayStyle.None;


        if (btnStart != null)
            btnStart.RegisterCallback<ClickEvent>(OnNextClicked);

    }

    private void OnNextClicked(ClickEvent evt)
    {

        btnStart?.SetEnabled(false);

        if (onboardingElement != null)
        {
            //onboardingElement.EnableInClassList("screen-visible", false);
            //onboardingElement.EnableInClassList("screen-hidden", true);
            onboardingElement.style.display = DisplayStyle.None;
        }


        loadingTimer = 0f;
        router.ShowWelcome();
        routerManager.SetActive(false);
    }

    private void Update()
    {
        if (loadingSpinner == null) return;

        bool spinnerVisible = loadingSpinner.resolvedStyle.display != DisplayStyle.None;

        if (spinnerVisible)
            SpinLoader();

        if (!spinnerVisible) return;

        loadingTimer += Time.deltaTime;
        if (loadingTimer >= loadingDuration)
        {
            loadingTimer = -999f;
            GoToMainOnboarding();
            loadingSpinner.style.display = DisplayStyle.None;
        }

        if (onboardingElement.resolvedStyle.display != DisplayStyle.None) {
            LoadingScreenElement.style.display = DisplayStyle.None;
            Debug.Log($"Trang thai loading: {loadingSpinner.style.display}");
        }
    }

    private void GoToMainOnboarding()
    {
        if (onboardingElement != null)
        {
            onboardingElement.style.display = DisplayStyle.Flex;

            LoadingScreenElement?.EnableInClassList("screen-visible", false);
            LoadingScreenElement?.EnableInClassList("screen-hidden", true);

            onboardingElement?.EnableInClassList("screen-visible", true);
            onboardingElement?.EnableInClassList("screen-hidden", false);

        }

        if (loadingSpinner != null)
            loadingSpinner.style.display = DisplayStyle.None;

        Debug.Log("Loading complete → chuyển sang màn Onboarding");
    }

    private void SpinLoader()
    {
        float speed = 180f;
        spinnerAngle += speed * Time.deltaTime;
        if (spinnerAngle >= 360f) spinnerAngle -= 360f;

        loadingSpinner.style.rotate = new Rotate(Angle.Degrees(spinnerAngle));
    }

    private void OnDestroy()
    {
        if (btnStart != null)
            btnStart.UnregisterCallback<ClickEvent>(OnNextClicked);
    }
}