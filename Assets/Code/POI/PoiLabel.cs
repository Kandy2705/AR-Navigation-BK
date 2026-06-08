using TMPro;
using UnityEngine;

/// <summary>
/// Nhãn tên nổi (billboard) phía trên capsule POI. Đọc tên thẳng từ
/// <see cref="TargetAnchor.displayName"/> (mà PoiSpawner đã set từ PoiData) — KHÔNG cần POI.cs.
///
/// Thay thế POISign: PoiLabel tự tạo TextMeshPro 3D child lúc runtime (không cần dựng
/// world-space Canvas thủ công). Chỉ cần add component này vào prefab capsule.
///
/// Tự ẩn/hiện theo capsule: TargetAnchor.SetVisible() toggle mọi Renderer con (gồm cả TMP của
/// label này) nên label tự đồng bộ visibility với capsule theo khoảng cách.
/// </summary>
public class PoiLabel : MonoBehaviour
{
    [Header("Label")]
    [Tooltip("Chiều cao label phía trên gốc capsule (mét).")]
    [SerializeField] private float heightOffset = 2.2f;

    [Tooltip("Cỡ chữ (world units). Tăng nếu chữ quá nhỏ khi ở xa.")]
    [SerializeField] private float fontSize = 4f;

    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Optional: TMP font asset. Để trống = dùng default font của TMP.")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Tooltip("Hiện thêm khoảng cách (m) tới user dưới tên.")]
    [SerializeField] private bool showDistance = true;

    [Header("Billboard")]
    [Tooltip("Chỉ xoay quanh trục Y (chữ luôn thẳng đứng, không nghiêng theo cao độ camera).")]
    [SerializeField] private bool yAxisOnly = true;

    [Tooltip("Nếu chữ hiển thị ngược (mirror) trên device, bật cái này để xoay 180°.")]
    [SerializeField] private bool flipFacing = false;

    private TargetAnchor _anchor;
    private TextMeshPro _tmp;
    private Transform _labelTransform;
    private Camera _cam;

    // Tên do PoiSpawner đẩy vào (authoritative) — ưu tiên hơn đọc từ anchor, tránh timing bug.
    private string _displayName;

    private void Awake()
    {
        _anchor = GetComponent<TargetAnchor>();
        if (_anchor == null) _anchor = GetComponentInParent<TargetAnchor>();
        CreateLabel();
    }

    /// <summary>
    /// PoiSpawner gọi NGAY sau khi spawn để đẩy tên chính xác (poi.displayName) vào label.
    /// Loại bỏ phụ thuộc thứ tự Awake (prefab có thể baked displayName cũ như "B9").
    /// </summary>
    public void SetDisplayName(string displayName)
    {
        _displayName = displayName;
        if (_tmp != null) _tmp.text = ResolveName();
    }

    /// <summary>Tên ưu tiên: pushed > anchor.displayName > gameObject.name.</summary>
    private string ResolveName()
    {
        if (!string.IsNullOrWhiteSpace(_displayName)) return _displayName;
        if (_anchor != null) return _anchor.TargetName;
        return gameObject.name;
    }

    private void CreateLabel()
    {
        var go = new GameObject("PoiLabel_Text");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, heightOffset, 0f);
        _labelTransform = go.transform;

        _tmp = go.AddComponent<TextMeshPro>();
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.fontSize = fontSize;
        _tmp.color = textColor;
        _tmp.enableWordWrapping = false;
        _tmp.text = ResolveName();
        if (fontAsset != null) _tmp.font = fontAsset;

        // RectTransform của TextMeshPro 3D — cho rộng để không bị cắt chữ
        var rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(12f, 3f);
    }

    private void LateUpdate()
    {
        if (_tmp == null || _labelTransform == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // 1. Cập nhật text
        string poiName = ResolveName();
        if (showDistance)
        {
            Vector3 camXZ = new Vector3(_cam.transform.position.x, 0f, _cam.transform.position.z);
            Vector3 selfXZ = new Vector3(transform.position.x, 0f, transform.position.z);
            float dist = Vector3.Distance(camXZ, selfXZ);
            _tmp.text = $"{poiName}\n<size=70%>{dist:F0} m</size>";
        }
        else
        {
            _tmp.text = poiName;
        }

        // 2. Billboard — quay mặt label về camera
        Vector3 dir = _labelTransform.position - _cam.transform.position; // cam → label
        if (yAxisOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;

        Quaternion look = Quaternion.LookRotation(dir.normalized);
        if (flipFacing) look *= Quaternion.Euler(0f, 180f, 0f);
        _labelTransform.rotation = look;
    }
}
