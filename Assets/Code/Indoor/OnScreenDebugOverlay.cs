using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// On-screen debug overlay — hiện thông tin realtime trên màn hình device.
/// Không cần adb, không cần USB. Chỉ cần build Development.
///
/// Hiển thị:
///   - HybridMode (Outdoor/Indoor/Transition)
///   - CurrentBuilding (None/B9/B10)
///   - agent.isOnNavMesh
///   - NavMesh vertex count
///   - Camera.main position
///   - MapLocalizationManager state
///   - POI count + collider status
///
/// Gắn lên Indoor Bootstrap hoặc bất kỳ GO persistent.
/// Tự ẩn khi build Release (chỉ hiện khi Development Build).
/// </summary>
[DisallowMultipleComponent]
public class OnScreenDebugOverlay : MonoBehaviour
{
    [SerializeField] private bool showInEditor = true;
    [SerializeField] private int fontSize = 28;
    [SerializeField] private Color textColor = Color.green;
    [SerializeField] private Color bgColor = new Color(0, 0, 0, 0.7f);

    private string _debugText = "";
    private float _updateInterval = 0.5f;
    private float _nextUpdate;
    private GUIStyle _style;
    private GUIStyle _bgStyle;
    private Texture2D _bgTex;

    private void OnEnable()
    {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
        enabled = false;
        return;
#endif
    }

    private void Update()
    {
        if (Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + _updateInterval;
        _debugText = BuildDebugText();
    }

    private void OnGUI()
    {
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
        return;
#endif
        if (!showInEditor && Application.isEditor) return;

        if (_style == null)
        {
            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, bgColor);
            _bgTex.Apply();

            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = true,
                wordWrap = true
            };
            _style.normal.textColor = textColor;

            _bgStyle = new GUIStyle();
            _bgStyle.normal.background = _bgTex;
        }

        float width = Screen.width * 0.95f;
        float height = _style.CalcHeight(new GUIContent(_debugText), width) + 20f;
        Rect rect = new Rect(10, Screen.height - height - 10, width, height);

        GUI.Box(rect, GUIContent.none, _bgStyle);
        GUI.Label(new Rect(rect.x + 10, rect.y + 5, rect.width - 20, rect.height - 10), _debugText, _style);
    }

    private string BuildDebugText()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>[DEBUG]</b> ");

        // 1. Hybrid Mode
        var hybrid = FindFirstObjectByType<HybridModeController>(FindObjectsInactive.Include);
        string mode = hybrid != null ? hybrid.CurrentMode.ToString() : "N/A";
        sb.Append($"Mode:<color=yellow>{mode}</color> | ");

        // 2. Current Building
        var switcher = FindFirstObjectByType<IndoorMapSwitcher>(FindObjectsInactive.Include);
        string building = switcher != null ? switcher.CurrentBuilding.ToString() : "N/A";
        sb.Append($"Building:<color=yellow>{building}</color>\n");

        // 3. Agent
        var navCtrl = NavigationController.instance;
        if (navCtrl == null)
        {
            sb.Append("NavCtrl: <color=red>NULL</color> | ");
        }
        else if (navCtrl.agent == null)
        {
            sb.Append("Agent: <color=red>NULL</color> | ");
        }
        else
        {
            bool onMesh = navCtrl.agent.isOnNavMesh;
            string meshColor = onMesh ? "green" : "red";
            sb.Append($"Agent.onNavMesh:<color={meshColor}>{onMesh}</color> | ");
            sb.Append($"Pos:{navCtrl.agent.transform.position.ToString("F1")} | ");
        }

        // 4. PathEstimationUtils
        var pathUtils = PathEstimationUtils.instance;
        sb.Append($"PathUtils:<color={(pathUtils != null ? "green" : "red")}>{(pathUtils != null ? "OK" : "NULL")}</color>\n");

        // 5. Camera
        var cam = Camera.main;
        if (cam != null)
        {
            sb.Append($"Cam:{cam.transform.position.ToString("F1")} | ");
            var col = cam.GetComponent<SphereCollider>();
            sb.Append($"SphereCol:<color={(col != null ? "green" : "red")}>{(col != null ? "OK" : "NULL")}</color> | ");
        }
        else
        {
            sb.Append("Cam:<color=red>NULL</color> | ");
        }

        // 6. NavMesh data
        var tri = NavMesh.CalculateTriangulation();
        sb.Append($"NavMesh:{tri.vertices.Length}v/{tri.indices.Length / 3}tri\n");

        // 7. Map Space transform
        Transform mapSpace = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t != null && t.name == "Map Space" && t.gameObject.scene.IsValid())
            { mapSpace = t; break; }
        }
        if (mapSpace != null)
        {
            sb.Append($"MapSpace pos:{mapSpace.position.ToString("F2")} rot:{mapSpace.eulerAngles.ToString("F1")}");
        }

        return sb.ToString();
    }
}
