using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the minimap <see cref="RawImage"/> stretched to the full circular mask rect.
/// Fixes cases where the render texture only appears in part of the circle (wrong layout / uvRect).
/// </summary>
[RequireComponent(typeof(RawImage))]
[DisallowMultipleComponent]
public class MinimapRawViewFill : MonoBehaviour
{
    private RectTransform _rect;
    private RawImage _raw;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _raw  = GetComponent<RawImage>();
    }

    void OnEnable()
    {
        Apply();
    }

    IEnumerator Start()
    {
        Apply();
        yield return null;
        Apply();
        Canvas.ForceUpdateCanvases();
    }

    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && _rect != null && _raw != null)
            Apply();
    }

    private void Apply()
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_raw == null)  _raw  = GetComponent<RawImage>();

        _rect.anchorMin        = Vector2.zero;
        _rect.anchorMax        = Vector2.one;
        _rect.pivot            = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = Vector2.zero;
        _rect.offsetMin        = Vector2.zero;
        _rect.offsetMax        = Vector2.zero;
        _rect.localScale       = Vector3.one;

        _raw.uvRect = new Rect(0f, 0f, 1f, 1f);
    }
}
