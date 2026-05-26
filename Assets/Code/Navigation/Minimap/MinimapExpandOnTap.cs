using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tap the satellite minimap to expand the <b>map view</b> (masked satellite area), not a thick outer ring.
/// Tap the dim backdrop to collapse. Optionally zooms the top-down minimap camera while expanded.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class MinimapExpandOnTap : MonoBehaviour
{
    [Tooltip("Đường kính vùng bản đồ (Minimap Circle Mask / vệ tinh) khi mở — tỷ lệ cạnh ngắn của Canvas (0.5 ≈ nửa màn). Viền xanh chỉ cộng thêm Rim bên dưới.")]
    [SerializeField] [Range(0.25f, 0.9f)] private float expandedScreenFraction = 0.5f;

    [Tooltip(
        "Độ dày viền Border quanh vùng map khi đang mở (pixel theo layout RectTransform, ~reference 1080). Giữ nhỏ để không ‘nuốt’ view.")]
    [SerializeField] [Range(4f, 32f)] private float expandedBorderRimPx = 10f;

    [Tooltip("Nhân orthographicSize khi mở rộng (&lt;1 = zoom gần mặt đất hơn). Mặc định 1 = chỉ phóng UI; bật &lt;1 nếu muốn “phóng” thêm nội dung camera.")]
    [SerializeField] [Range(0.35f, 1f)] private float expandedOrthoMultiplier = 1f;

    [SerializeField] private MinimapTopDownCamera minimapTopDownCamera;

    private RectTransform _canvasRect;
    private Canvas _canvas;

    private RectTransform _mask;
    private RectTransform _border;
    private bool _nestedMaskUnderBorder;

    private Vector2 _collapsedBorderSize;
    private Vector2 _collapsedBorderPos;
    private Vector2 _collapsedMaskSize;
    private Vector2 _collapsedMaskPos;
    private Vector2 _collapsedMaskAnchorMin;
    private Vector2 _collapsedMaskAnchorMax;
    private Vector2 _collapsedMaskPivot;
    private Vector2 _collapsedMaskOffsetMin;
    private Vector2 _collapsedMaskOffsetMax;
    private bool _hasCollapsedMaskLayout;

    private float _ringExtraPx;

    private GameObject _backdrop;
    private bool _expanded;
    private float _orthoBeforeExpand = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!GpsOutdoorSceneNames.ShouldAutoSpawnMinimapHeadingIndicator(scene.name)) return;

        RectTransform maskRt = null;
        foreach (RectTransform rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rt == null || rt.gameObject.scene != scene) continue;
            if (rt.name != "Minimap Circle Mask") continue;
            maskRt = rt;
            break;
        }

        if (maskRt == null) return;

        RectTransform host = maskRt;
        if (maskRt.parent != null && maskRt.parent.name == "Minimap Border")
            host = maskRt.parent as RectTransform;

        if (host == null || host.GetComponent<MinimapExpandOnTap>() != null) return;
        if (host.GetComponent<Image>() == null) return;

        host.gameObject.AddComponent<MinimapExpandOnTap>();
    }

    private void Awake()
    {
        RectTransform host = transform as RectTransform;
        if (host.name == "Minimap Border")
        {
            _border = host;
            _mask = FindNamedDescendant(_border, "Minimap Circle Mask");
            _nestedMaskUnderBorder = _mask != null;
        }
        else
        {
            _mask = host;
            ResolveBorderLayout();
        }

        _canvas = (_nestedMaskUnderBorder ? _border : _mask).GetComponentInParent<Canvas>();
        if (_canvas != null)
            _canvasRect = _canvas.transform as RectTransform;

        Image hostImg = host.GetComponent<Image>();
        if (hostImg != null)
            hostImg.raycastTarget = true;

        if (minimapTopDownCamera == null)
            minimapTopDownCamera = FindFirstObjectByType<MinimapTopDownCamera>(FindObjectsInactive.Include);

        DisableLabelRaycasts();
    }

    private void ResolveBorderLayout()
    {
        Transform parent = _mask != null ? _mask.parent : null;
        if (parent != null && parent.name == "Minimap Border")
        {
            _nestedMaskUnderBorder = true;
            _border = parent as RectTransform;
        }
        else
        {
            _nestedMaskUnderBorder = false;
            _border = FindSiblingNamed(_mask, "Minimap Border");
        }
    }

    private static RectTransform FindNamedDescendant(RectTransform root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == objectName)
                return t as RectTransform;
        }

        return null;
    }

    private static RectTransform FindSiblingNamed(RectTransform self, string name)
    {
        if (self == null || self.parent == null) return null;
        foreach (Transform t in self.parent)
        {
            if (t.name == name)
                return t as RectTransform;
        }

        return null;
    }

    private void DisableLabelRaycasts()
    {
        if (_mask == null) return;
        foreach (Text t in _mask.GetComponentsInChildren<Text>(true))
        {
            if (t != null)
                t.raycastTarget = false;
        }
    }

    private void Start()
    {
        StartCoroutine(DeferredSetup());
    }

    private System.Collections.IEnumerator DeferredSetup()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        CaptureCollapsedLayout();
        EnsureBackdrop();
        WireTapRelays();
    }

    private void CaptureCollapsedLayout()
    {
        _hasCollapsedMaskLayout = false;

        if (_nestedMaskUnderBorder && _border != null)
        {
            _collapsedBorderSize = _border.sizeDelta;
            _collapsedBorderPos = _border.anchoredPosition;
            if (_mask != null)
            {
                _collapsedMaskSize = _mask.sizeDelta;
                _collapsedMaskPos = _mask.anchoredPosition;
                _collapsedMaskAnchorMin = _mask.anchorMin;
                _collapsedMaskAnchorMax = _mask.anchorMax;
                _collapsedMaskPivot = _mask.pivot;
                _collapsedMaskOffsetMin = _mask.offsetMin;
                _collapsedMaskOffsetMax = _mask.offsetMax;
                _hasCollapsedMaskLayout = true;
            }

            _ringExtraPx = _mask != null ? Mathf.Max(0f, _border.sizeDelta.x - _mask.rect.width) : 0f;
            return;
        }

        if (_mask != null)
        {
            _collapsedMaskSize = _mask.sizeDelta;
            _collapsedMaskPos = _mask.anchoredPosition;
            _collapsedMaskAnchorMin = _mask.anchorMin;
            _collapsedMaskAnchorMax = _mask.anchorMax;
            _collapsedMaskPivot = _mask.pivot;
            _collapsedMaskOffsetMin = _mask.offsetMin;
            _collapsedMaskOffsetMax = _mask.offsetMax;
            _hasCollapsedMaskLayout = true;
        }

        if (_border != null)
        {
            _collapsedBorderSize = _border.sizeDelta;
            _collapsedBorderPos = _border.anchoredPosition;
            _ringExtraPx = Mathf.Max(0f, _border.sizeDelta.x - _collapsedMaskSize.x);
        }
        else
        {
            _ringExtraPx = 0f;
        }
    }

    private void EnsureBackdrop()
    {
        if (_backdrop != null || _canvas == null) return;

        GameObject go = new GameObject("Minimap Expand Backdrop",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(MinimapExpandBackdrop));
        go.transform.SetParent(_canvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.02f);
        img.raycastTarget = true;

        go.GetComponent<MinimapExpandBackdrop>().Owner = this;
        go.SetActive(false);
        _backdrop = go;
    }

    /// <summary>Forwarded from <see cref="MinimapExpandHitRelay"/> on RawImage / Border (UI does not bubble clicks to parents).</summary>
    internal void RequestExpandFromTap()
    {
        if (!_expanded)
            Expand();
    }

    internal void OnBackdropClicked()
    {
        if (_expanded)
            Collapse();
    }

    private void WireTapRelays()
    {
        var wired = new HashSet<GameObject>();
        MinimapExpandOnTap owner = this;

        if (_mask != null)
        {
            RawImage raw = _mask.GetComponentInChildren<RawImage>(true);
            if (raw != null)
                AddRelay(raw.gameObject, wired, owner);
        }

        if (_border != null)
            AddRelay(_border.gameObject, wired, owner);
    }

    private static void AddRelay(GameObject go, HashSet<GameObject> wired, MinimapExpandOnTap owner)
    {
        if (go == null || wired.Contains(go) || owner == null) return;
        wired.Add(go);

        Image img = go.GetComponent<Image>();
        if (img != null)
            img.raycastTarget = true;

        MinimapExpandHitRelay relay = go.GetComponent<MinimapExpandHitRelay>();
        if (relay == null)
            relay = go.AddComponent<MinimapExpandHitRelay>();

        relay.Owner = owner;
    }

    private void Expand()
    {
        if (_expanded || _canvasRect == null) return;

        Canvas.ForceUpdateCanvases();
        float minSide = Mathf.Min(_canvasRect.rect.width, _canvasRect.rect.height);
        if (minSide < 1f)
            minSide = Mathf.Min(Screen.width, Screen.height);

        float viewDiameter = minSide * expandedScreenFraction;
        float rim = expandedBorderRimPx;
        float outerDiameter = viewDiameter + rim;

        if (_nestedMaskUnderBorder && _border != null)
        {
            _border.sizeDelta = new Vector2(outerDiameter, outerDiameter);
            _border.anchoredPosition = _collapsedBorderPos;

            if (_mask != null)
            {
                // Giữ Mask stretch trong Border — chỉ inset viền mỏng. Tránh anchor 0.5+sizeDelta (dễ làm mất RawImage / vùng clip).
                float halfRim = rim * 0.5f;
                _mask.anchorMin = Vector2.zero;
                _mask.anchorMax = Vector2.one;
                _mask.pivot = new Vector2(0.5f, 0.5f);
                _mask.anchoredPosition = Vector2.zero;
                _mask.offsetMin = new Vector2(halfRim, halfRim);
                _mask.offsetMax = new Vector2(-halfRim, -halfRim);
            }
        }
        else
        {
            if (_mask != null)
            {
                _mask.sizeDelta = new Vector2(viewDiameter, viewDiameter);
                _mask.anchoredPosition = _collapsedMaskPos;
            }

            if (_border != null)
            {
                _border.sizeDelta = new Vector2(outerDiameter, outerDiameter);
                _border.anchoredPosition = _collapsedBorderPos;
            }
        }

        ApplyOrthoForExpanded(true);
        if (_backdrop != null)
        {
            _backdrop.SetActive(true);
            _backdrop.transform.SetAsFirstSibling();
        }

        BringMinimapClusterToFront();
        _expanded = true;
    }

    private void Collapse()
    {
        if (!_expanded) return;

        if (_nestedMaskUnderBorder && _border != null)
        {
            _border.sizeDelta = _collapsedBorderSize;
            _border.anchoredPosition = _collapsedBorderPos;

            if (_mask != null && _hasCollapsedMaskLayout)
            {
                _mask.anchorMin = _collapsedMaskAnchorMin;
                _mask.anchorMax = _collapsedMaskAnchorMax;
                _mask.pivot = _collapsedMaskPivot;
                _mask.sizeDelta = _collapsedMaskSize;
                _mask.anchoredPosition = _collapsedMaskPos;
                _mask.offsetMin = _collapsedMaskOffsetMin;
                _mask.offsetMax = _collapsedMaskOffsetMax;
            }
        }
        else
        {
            if (_mask != null && _hasCollapsedMaskLayout)
            {
                _mask.anchorMin = _collapsedMaskAnchorMin;
                _mask.anchorMax = _collapsedMaskAnchorMax;
                _mask.pivot = _collapsedMaskPivot;
                _mask.sizeDelta = _collapsedMaskSize;
                _mask.anchoredPosition = _collapsedMaskPos;
                _mask.offsetMin = _collapsedMaskOffsetMin;
                _mask.offsetMax = _collapsedMaskOffsetMax;
            }

            if (_border != null)
            {
                _border.sizeDelta = _collapsedBorderSize;
                _border.anchoredPosition = _collapsedBorderPos;
            }
        }

        ApplyOrthoForExpanded(false);
        if (_backdrop != null)
            _backdrop.SetActive(false);

        _expanded = false;
    }

    private void BringMinimapClusterToFront()
    {
        if (_nestedMaskUnderBorder)
        {
            if (_border != null)
                _border.SetAsLastSibling();
        }
        else
        {
            if (_border != null)
                _border.SetAsLastSibling();
            if (_mask != null)
                _mask.SetAsLastSibling();
        }
    }

    private void ApplyOrthoForExpanded(bool expandPhase)
    {
        if (minimapTopDownCamera == null || Mathf.Approximately(expandedOrthoMultiplier, 1f))
            return;

        Camera cam = minimapTopDownCamera.GetComponent<Camera>();
        if (cam == null) return;

        if (expandPhase)
        {
            if (_orthoBeforeExpand < 0f)
                _orthoBeforeExpand = cam.orthographicSize;
            float target = _orthoBeforeExpand * expandedOrthoMultiplier;
            minimapTopDownCamera.SetRuntimeOrthographicSize(target);
        }
        else
        {
            minimapTopDownCamera.RestoreInspectorViewRadius();
            _orthoBeforeExpand = -1f;
        }
    }

    private void OnDisable()
    {
        if (_expanded)
            Collapse();
    }
}

/// <summary>Forwards pointer clicks to <see cref="MinimapExpandOnTap"/> (needed because RawImage consumes raycasts).</summary>
[DisallowMultipleComponent]
public class MinimapExpandHitRelay : MonoBehaviour, IPointerClickHandler
{
    public MinimapExpandOnTap Owner { get; set; }

    public void OnPointerClick(PointerEventData eventData)
    {
        Owner?.RequestExpandFromTap();
    }
}

/// <summary>Full-screen hit target behind the minimap; receives taps outside the circle.</summary>
[DisallowMultipleComponent]
public class MinimapExpandBackdrop : MonoBehaviour, IPointerClickHandler
{
    public MinimapExpandOnTap Owner { get; set; }

    public void OnPointerClick(PointerEventData eventData)
    {
        Owner?.OnBackdropClicked();
    }
}
