namespace ARNavB9V2.UI
{
    /// <summary>
    /// What the navigation HUD shows, computed by <see cref="B9NavigationHud"/> from the navigation controllers.
    /// It holds text and visibility only, so any view (the Canvas prefab, a test) can draw it.
    /// A refresh only rewrites the fields its state needs: the others keep their last value, as the labels did.
    /// Setters are public so tests and previews can describe a state without the navigation scene.
    /// </summary>
    public sealed class B9NavigationHudState
    {
        public const string InitialStatus = "Đang chuẩn bị GPS…";
        public const string InitialDetail = "SchoolGround · GPS";
        public const string InitialSummary = "Ngoài trời → cửa B9 → phòng đã chọn";
        public const string InitialExperiment = "LOG · đang chuẩn bị";
        public const string SaveTrialLabel = "LƯU LẦN THỬ";
        public const string NewTrialLabel = "BẮT ĐẦU LẦN MỚI";
        public const string CollapsedMinimapHint = "CHẠM XEM TỔNG QUAN";
        public const string ExpandedMinimapHint = "CHẠM ĐỂ THU NHỎ";

        /// <summary>Main line ("Đi theo mũi tên tới B9-104").</summary>
        public string Status { get; set; } = InitialStatus;

        /// <summary>Tracking line under the status (GPS accuracy, PDR distance, VPS hint).</summary>
        public string Detail { get; set; } = InitialDetail;

        /// <summary>Where the route goes next ("Đang đi tới cửa B9 trước · đích cuối: B9-104").</summary>
        public string Summary { get; set; } = InitialSummary;

        public string HarmonyProfile { get; set; } = "HARMONY V5 · Full HARMONY";

        /// <summary>Version code of the active HARMONY profile; the matching selector button is highlighted.</summary>
        public string ActiveVersionCode { get; set; } = string.Empty;

        public string Experiment { get; set; } = InitialExperiment;
        public string ExperimentToggleLabel { get; set; } = SaveTrialLabel;
        public bool ExperimentToggleEnabled { get; set; } = true;
        public bool MarkerEnabled { get; set; } = true;
        public bool ExportEnabled { get; set; } = true;

        public bool RetryVisible { get; set; }
        public bool StartVisible { get; set; } = true;
        public bool CancelVisible { get; set; } = true;
        public bool ExitVisible { get; set; }

        public bool MinimapExpanded { get; set; }
        public string MinimapHint { get; set; } = CollapsedMinimapHint;

        /// <summary>Room id or campus place the user is being guided to; empty when no destination was applied.</summary>
        public string ActiveDestination { get; set; } = string.Empty;
    }
}
