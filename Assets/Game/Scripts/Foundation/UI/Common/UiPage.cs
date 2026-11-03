using System;
using System.Collections;
using UnityEngine;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Base class for a Canvas screen. One prefab per screen, one subclass per prefab.
    /// <see cref="NavigationManager"/> shows and hides pages; subclasses wire their widgets in
    /// <see cref="OnShow"/> / <see cref="OnHide"/> through serialized references (no queries by name).
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public class UiPage : MonoBehaviour
    {
        [SerializeField] private PageID pageId = PageID.None;

        [Tooltip("Slide + fade duration. 0 disables the transition.")]
        [SerializeField, Min(0f)] private float transitionSeconds = 0.25f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Coroutine transition;

        public PageID Id => pageId;
        public NavigationManager Navigation { get; private set; }
        public bool IsVisible { get; private set; }

        public event Action<UiPage> Shown;
        public event Action<UiPage> Hidden;

        public void Attach(NavigationManager navigation)
        {
            Navigation = navigation;
        }

        /// <param name="fromLeft">True when navigating back: the page enters from the left.</param>
        public void Show(bool fromLeft = false)
        {
            CacheComponents();
            StopTransition();
            gameObject.SetActive(true);
            IsVisible = true;
            OnShow();
            Shown?.Invoke(this);

            if (!CanAnimate())
            {
                ApplyPose(0f, 1f);
                canvasGroup.interactable = true;
                return;
            }

            float width = rectTransform.rect.width;
            transition = StartCoroutine(Animate(fromLeft ? -width : width, 0f, 0f, 1f, hideWhenDone: false));
        }

        /// <param name="toRight">True when navigating back: the page leaves to the right.</param>
        public void Hide(bool toRight = false)
        {
            CacheComponents();
            if (!gameObject.activeSelf)
            {
                IsVisible = false;
                return;
            }

            StopTransition();
            IsVisible = false;
            OnHide();

            if (!CanAnimate())
            {
                FinishHide();
                return;
            }

            float width = rectTransform.rect.width;
            transition = StartCoroutine(Animate(0f, toRight ? width : -width, 1f, 0f, hideWhenDone: true));
        }

        /// <summary>Hides without animation and without calling <see cref="OnHide"/>; used when resetting the whole UI.</summary>
        public void HideImmediate()
        {
            CacheComponents();
            StopTransition();
            IsVisible = false;
            FinishHide();
        }

        protected virtual void OnShow() { }

        protected virtual void OnHide() { }

        private void OnDisable()
        {
            transition = null;
        }

        private bool CanAnimate()
        {
            return transitionSeconds > 0f && Application.isPlaying && gameObject.activeInHierarchy;
        }

        private IEnumerator Animate(float fromX, float toX, float fromAlpha, float toAlpha, bool hideWhenDone)
        {
            canvasGroup.interactable = false;
            ApplyPose(fromX, fromAlpha);

            float elapsed = 0f;
            while (elapsed < transitionSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionSeconds));
                ApplyPose(Mathf.Lerp(fromX, toX, k), Mathf.Lerp(fromAlpha, toAlpha, k));
                yield return null;
            }

            transition = null;
            if (hideWhenDone)
            {
                FinishHide();
                yield break;
            }

            ApplyPose(toX, toAlpha);
            canvasGroup.interactable = true;
        }

        private void FinishHide()
        {
            ApplyPose(0f, 1f);
            canvasGroup.interactable = true;
            gameObject.SetActive(false);
            Hidden?.Invoke(this);
        }

        private void ApplyPose(float x, float alpha)
        {
            rectTransform.anchoredPosition = new Vector2(x, rectTransform.anchoredPosition.y);
            canvasGroup.alpha = alpha;
        }

        private void StopTransition()
        {
            if (transition == null)
            {
                return;
            }

            StopCoroutine(transition);
            transition = null;
        }

        private void CacheComponents()
        {
            if (rectTransform == null)
            {
                rectTransform = (RectTransform)transform;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}
