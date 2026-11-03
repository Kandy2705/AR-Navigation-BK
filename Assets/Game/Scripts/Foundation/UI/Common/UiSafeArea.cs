using UnityEngine;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Fits this RectTransform to <see cref="Screen.safeArea"/> (notch, home bar). Put it on a content
    /// child of a page so full-bleed backgrounds can still extend behind the notch.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiSafeArea : MonoBehaviour
    {
        [SerializeField] private bool conformX = true;
        [SerializeField] private bool conformY = true;

        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect area = Screen.safeArea;
            Vector2 min = area.position;
            Vector2 max = area.position + area.size;

            if (!conformX)
            {
                min.x = 0f;
                max.x = Screen.width;
            }

            if (!conformY)
            {
                min.y = 0f;
                max.y = Screen.height;
            }

            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            // Some Android devices report NaN on the first frame.
            if (float.IsNaN(min.x) || float.IsNaN(min.y) || float.IsNaN(max.x) || float.IsNaN(max.y))
            {
                return;
            }

            var rect = (RectTransform)transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            lastSafeArea = area;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
