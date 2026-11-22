using System.Collections.Generic;
using ARNavB9V2.UI;
using Game.Foundation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Features.Home
{
    /// <summary>
    /// The guidance overlay of the B9 navigation scene, drawn with uGUI: a status card (what to do, tracking,
    /// where the route goes), a round minimap that expands on tap, the destination picker with start / cancel /
    /// exit, and a research panel (HARMONY version, trial log, CSV export). It only draws
    /// <see cref="IB9NavigationHudModel"/> and forwards taps to it; the navigation rules stay in <see cref="B9NavigationHud"/>.
    /// Inside the app shell it leaves the top bar and the assistant to <see cref="ArHudUI"/> and hides its own cancel button.
    /// </summary>
    public class NavigationHudUI : MonoBehaviour
    {
        public const string PickerTitle = "Chọn điểm đến";
        public const string NoDestinationLabel = "Chọn điểm đến";

        [Tooltip("HUD logic in the navigation scene. Found at start when empty.")]
        [SerializeField] private B9NavigationHud hud;
        [SerializeField] private Canvas canvas;

        [Header("Status")]
        [SerializeField] private RectTransform statusCard;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private Button researchButton;
        [SerializeField] private Button retryButton;

        [Header("Minimap")]
        [SerializeField] private RectTransform minimapFrame;
        [SerializeField] private Button minimapButton;
        [SerializeField] private RawImage minimapImage;
        [SerializeField] private TMP_Text minimapHint;

        [Header("Destination")]
        [SerializeField] private RectTransform destinationPanel;
        [SerializeField] private Button destinationField;
        [SerializeField] private TMP_Text destinationLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject actionsRow;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private UiPickerSheet picker;

        [Header("Research")]
        [SerializeField] private GameObject researchPanel;
        [SerializeField] private Button researchCloseButton;
        [SerializeField] private TMP_Text harmonyLabel;
        [SerializeField] private RectTransform versionGrid;
        [SerializeField] private Button versionTemplate;
        [SerializeField] private TMP_Text experimentLabel;
        [SerializeField] private Button markerButton;
        [SerializeField] private Button trialButton;
        [SerializeField] private TMP_Text trialLabel;
        [SerializeField] private Button exportButton;

        [Header("Layout (frame units)")]
        [Tooltip("Status card top inside the app shell, below the AR page's buttons.")]
        [SerializeField] private float shellTop = 104f;
        [SerializeField] private float standaloneTop = 20f;
        [Tooltip("Destination panel bottom inside the app shell, above the assistant and its search bar.")]
        [SerializeField] private float shellBottom = 124f;
        [SerializeField] private float standaloneBottom = 20f;
        [SerializeField] private float sideMargin = 16f;
        [SerializeField] private float minimapGap = 12f;
        [SerializeField] private float minimapSize = 96f;
        [SerializeField] private float expandedMinimapSize = 300f;

        [Tooltip("Canvas order while the picker or research panel is open: above AppCanvas (100), so the AR page's assistant does not cover them.")]
        [SerializeField] private int overlaySortingOrder = 150;

        private readonly List<Button> versionButtons = new List<Button>();
        private readonly List<string> versionCodes = new List<string>();
        private IB9NavigationHudModel model;
        private int baseSortingOrder;

        public IB9NavigationHudModel Model => model;

        public bool IsShown => canvas == null || canvas.enabled;

        private void Awake()
        {
            researchButton.onClick.AddListener(OpenResearch);
            researchCloseButton.onClick.AddListener(CloseResearch);
            retryButton.onClick.AddListener(() => model?.RetryVps());
            minimapButton.onClick.AddListener(() => model?.ToggleMinimap());
            destinationField.onClick.AddListener(OpenPicker);
            startButton.onClick.AddListener(() => model?.StartNavigation());
            cancelButton.onClick.AddListener(() => model?.CancelNavigation());
            exitButton.onClick.AddListener(() => model?.ExitBuilding());
            markerButton.onClick.AddListener(() => model?.AddResearchMarker());
            trialButton.onClick.AddListener(() => model?.ToggleTrial());
            exportButton.onClick.AddListener(() => model?.ExportLogs());
            versionTemplate.gameObject.SetActive(false);
            researchPanel.SetActive(false);
            if (canvas != null && !canvas.isRootCanvas)
            {
                // Nested under another canvas (previews, tests) the order only counts with an override.
                canvas.overrideSorting = true;
            }

            baseSortingOrder = canvas != null ? canvas.sortingOrder : 0;
        }

        private void Update()
        {
            UpdateSorting();
        }

        /// <summary>True while the destination picker or the research panel covers the screen.</summary>
        public bool OverlayOpen => researchPanel.activeSelf || picker.IsVisible;

        private void OnEnable()
        {
            ArNavigationBridge.VisibilityChanged += OnBridgeChanged;
            ArNavigationBridge.ClearRouteRequested += OnClearRouteRequested;
            OnBridgeChanged();
        }

        private void OnDisable()
        {
            ArNavigationBridge.VisibilityChanged -= OnBridgeChanged;
            ArNavigationBridge.ClearRouteRequested -= OnClearRouteRequested;
        }

        private void Start()
        {
            if (model != null)
            {
                return;
            }

            if (hud == null)
            {
                hud = FindFirstObjectByType<B9NavigationHud>(FindObjectsInactive.Include);
            }

            if (hud != null)
            {
                Bind(hud);
            }
            else
            {
                Debug.LogWarning("[NavigationHudUI] No B9NavigationHud in the loaded scenes; the overlay stays empty.");
            }
        }

        private void OnDestroy()
        {
            if (model != null)
            {
                model.Changed -= Render;
            }
        }

        /// <summary>Draws <paramref name="source"/> and sends the taps to it (the scene's HUD, or a fake in tests).</summary>
        public void Bind(IB9NavigationHudModel source)
        {
            if (model != null)
            {
                model.Changed -= Render;
            }

            model = source;
            if (model != null)
            {
                model.Changed += Render;
            }

            BuildVersionButtons();
            Render();
        }

        public void OpenResearch()
        {
            researchPanel.SetActive(true);
            researchPanel.transform.SetAsLastSibling();
            UpdateSorting();
        }

        public void CloseResearch()
        {
            researchPanel.SetActive(false);
            UpdateSorting();
        }

        public void OpenPicker()
        {
            if (model == null || model.DestinationChoices.Count == 0)
            {
                return;
            }

            var options = new List<string>(model.DestinationChoices);
            var column = new UiPickerSheet.Column { Options = options, Selected = Mathf.Max(0, options.IndexOf(model.SelectedDestination)) };
            picker.Show(PickerTitle, new List<UiPickerSheet.Column> { column }, picked =>
            {
                if (picked.Length > 0 && picked[0] >= 0 && picked[0] < options.Count)
                {
                    model?.SelectDestination(options[picked[0]]);
                }
            });
            UpdateSorting();
        }

        /// <summary>Redraws everything from the model's current state.</summary>
        public void Render()
        {
            ApplyLayout();
            ApplyVisibility();
            if (model == null)
            {
                return;
            }

            B9NavigationHudState state = model.State;
            statusLabel.text = state.Status;
            detailLabel.text = state.Detail;
            summaryLabel.text = state.Summary;
            retryButton.gameObject.SetActive(state.RetryVisible);

            destinationLabel.text = string.IsNullOrEmpty(model.SelectedDestination) ? NoDestinationLabel : model.SelectedDestination;
            startButton.gameObject.SetActive(state.StartVisible);
            // The AR page has its own "Xóa chỉ đường"; a second cancel button would only confuse.
            cancelButton.gameObject.SetActive(state.CancelVisible && !ArNavigationBridge.ShellAttached);
            exitButton.gameObject.SetActive(state.ExitVisible);
            actionsRow.SetActive(cancelButton.gameObject.activeSelf || exitButton.gameObject.activeSelf);

            RenderMinimap(state);
            RenderResearch(state);
            ArNavigationBridge.PublishDestination(state.CancelVisible ? state.ActiveDestination : string.Empty);
        }

        private void RenderMinimap(B9NavigationHudState state)
        {
            Texture texture = model.MinimapTexture;
            minimapImage.texture = texture;
            minimapImage.color = texture != null ? Color.white : UiTheme.Surface;
            minimapHint.text = state.MinimapHint;

            float size = state.MinimapExpanded ? expandedMinimapSize : minimapSize;
            minimapFrame.sizeDelta = new Vector2(size, size);
            var parent = (RectTransform)minimapFrame.parent;
            float x = state.MinimapExpanded ? -(parent.rect.width - size) * 0.5f : -sideMargin;
            minimapFrame.anchoredPosition = new Vector2(x, minimapFrame.anchoredPosition.y);
            minimapFrame.SetAsLastSibling();
            researchPanel.transform.SetAsLastSibling();
            picker.transform.SetAsLastSibling();
        }

        private void RenderResearch(B9NavigationHudState state)
        {
            harmonyLabel.text = FormatHarmony(state.HarmonyProfile);
            experimentLabel.text = state.Experiment;
            trialLabel.text = state.ExperimentToggleLabel;
            trialButton.interactable = state.ExperimentToggleEnabled;
            markerButton.interactable = state.MarkerEnabled;
            exportButton.interactable = state.ExportEnabled;

            for (int i = 0; i < versionButtons.Count; i++)
            {
                bool selected = versionCodes[i] == state.ActiveVersionCode;
                versionButtons[i].targetGraphic.color = selected ? UiTheme.Accent : UiTheme.Surface;
            }
        }

        /// <summary>
        /// The profile marks enabled parts with "✓", which neither Poppins nor its Liberation fallback can draw;
        /// a green dot (in Liberation) stands in for it on screen.
        /// </summary>
        public static string FormatHarmony(string profile)
        {
            return string.IsNullOrEmpty(profile) ? string.Empty : profile.Replace("✓", "<color=#8CEBB8>●</color>");
        }

        private void BuildVersionButtons()
        {
            foreach (Button button in versionButtons)
            {
                if (button != null)
                {
                    UiGameObjects.Discard(button.gameObject);
                }
            }

            versionButtons.Clear();
            versionCodes.Clear();
            if (model == null)
            {
                return;
            }

            foreach (string code in model.HarmonyVersionCodes)
            {
                Button button = Instantiate(versionTemplate, versionGrid, false);
                button.name = "Version_" + code;
                button.gameObject.SetActive(true);
                button.GetComponentInChildren<TMP_Text>(true).text = code;
                string picked = code;
                button.onClick.AddListener(() => model?.SelectHarmonyVersion(picked));
                versionButtons.Add(button);
                versionCodes.Add(code);
            }
        }

        /// <summary>Inside the shell the card and panel move clear of the AR page's buttons and assistant.</summary>
        private void ApplyLayout()
        {
            bool shell = ArNavigationBridge.ShellAttached;
            float top = shell ? shellTop : standaloneTop;
            statusCard.anchoredPosition = new Vector2(statusCard.anchoredPosition.x, -top);
            float belowCard = top + statusCard.sizeDelta.y + minimapGap;
            minimapFrame.anchoredPosition = new Vector2(minimapFrame.anchoredPosition.x, -belowCard);
            var retry = (RectTransform)retryButton.transform;
            retry.anchoredPosition = new Vector2(retry.anchoredPosition.x, -belowCard);
            destinationPanel.anchoredPosition = new Vector2(destinationPanel.anchoredPosition.x, shell ? shellBottom : standaloneBottom);
        }

        private void UpdateSorting()
        {
            if (canvas == null)
            {
                return;
            }

            int order = OverlayOpen ? overlaySortingOrder : baseSortingOrder;
            if (canvas.sortingOrder != order)
            {
                canvas.sortingOrder = order;
            }
        }

        private void ApplyVisibility()
        {
            if (canvas != null)
            {
                canvas.enabled = ArNavigationBridge.HudVisible;
            }
        }

        private void OnBridgeChanged()
        {
            Render();
        }

        private void OnClearRouteRequested()
        {
            model?.CancelNavigation();
        }
    }
}
