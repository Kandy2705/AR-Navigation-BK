using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

        [Tooltip("Length of a slide (push/back) transition; fades take a little less. 0 disables transitions.")]
        [SerializeField, Min(0f)] private float transitionSeconds = UiMotion.PushSeconds;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Coroutine transition;
        private Image dimLayer;

        public PageID Id => pageId;
        public NavigationManager Navigation { get; private set; }
        public bool IsVisible { get; private set; }

        public event Action<UiPage> Shown;
        public event Action<UiPage> Hidden;

        public void Attach(NavigationManager navigation)
        {
            Navigation = navigation;
        }

        /// <param name="fromLeft">True when navigating back: the page is uncovered instead of sliding in.</param>
        public void Show(bool fromLeft = false)
        {
            Show(fromLeft ? PageTransition.Pop : PageTransition.Push);
        }

        public void Show(PageTransition kind)
        {
            CacheComponents();
            StopTransition();
            gameObject.SetActive(true);
            IsVisible = true;
            OnShow();
            Shown?.Invoke(this);

            if (!CanAnimate())
            {
                ApplyPose(PagePose.Rest);
                SetInput(true, true);
                return;
            }

            UiPressFeedback.AttachTo(transform);
            transition = StartCoroutine(Animate(kind, true));
        }

        /// <param name="toRight">True when navigating back: the page slides out to the right.</param>
        public void Hide(bool toRight = false)
        {
            Hide(toRight ? PageTransition.Pop : PageTransition.Push);
        }

        public void Hide(PageTransition kind)
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

            transition = StartCoroutine(Animate(kind, false));
        }

        /// <summary>Hides without animation and without calling <see cref="OnHide"/>; used when resetting the whole UI.</summary>
        public void HideImmediate()
        {
            CacheComponents();
            StopTransition();
            IsVisible = false;
            FinishHide();
        }

        /// <summary>Freezes the page at one frame of a transition; used by the motion preview renders.</summary>
        public void ApplyTransitionPose(PagePose pose)
        {
            CacheComponents();
            ApplyPose(pose);
        }

        /// <summary>Back arrow behaviour: previous page in the navigation history, if there is one.</summary>
        protected void GoBack()
        {
            if (Navigation != null)
            {
                Navigation.GoBack();
            }
        }

        /// <summary>Opens another page when it is in the router; logs instead of failing while a screen is not available.</summary>
        protected void OpenPage(PageID target)
        {
            if (Navigation != null && Navigation.HasRoute(target))
            {
                Navigation.Navigate(target);
            }
            else
            {
                Debug.Log($"[{GetType().Name}] {target} screen is not available.");
            }
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

        private IEnumerator Animate(PageTransition kind, bool entering)
        {
            // The arriving page takes the taps straight away; the leaving one lets them through.
            SetInput(false, entering);
            float width = rectTransform.rect.width;
            float seconds = transitionSeconds * UiMotion.Duration(kind) / UiMotion.PushSeconds;
            yield return UiMotion.Run(seconds, t => ApplyPose(UiMotion.Page(kind, entering, t, width)));

            transition = null;
            if (!entering)
            {
                FinishHide();
                yield break;
            }

            ApplyPose(PagePose.Rest);
            SetInput(true, true);
        }

        private void FinishHide()
        {
            ApplyPose(PagePose.Rest);
            SetInput(true, true);
            gameObject.SetActive(false);
            Hidden?.Invoke(this);
        }

        private void ApplyPose(PagePose pose)
        {
            rectTransform.anchoredPosition = new Vector2(pose.X, rectTransform.anchoredPosition.y);
            rectTransform.localScale = new Vector3(pose.Scale, pose.Scale, 1f);
            canvasGroup.alpha = pose.Alpha;
            SetDim(pose.Dim);
        }

        private void SetInput(bool interactable, bool blocksRaycasts)
        {
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = blocksRaycasts;
        }

        /// <summary>Dark layer over the whole page, used while another page covers it.</summary>
        private void SetDim(float strength)
        {
            if (strength <= 0.001f)
            {
                if (dimLayer != null && dimLayer.gameObject.activeSelf)
                {
                    dimLayer.gameObject.SetActive(false);
                }

                return;
            }

            if (dimLayer == null)
            {
                var layer = new GameObject("TransitionDim", typeof(RectTransform));
                var rect = (RectTransform)layer.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                dimLayer = layer.AddComponent<Image>();
                dimLayer.raycastTarget = false;
                layer.SetActive(false);
            }

            if (!dimLayer.gameObject.activeSelf)
            {
                dimLayer.transform.SetAsLastSibling();
                dimLayer.gameObject.SetActive(true);
            }

            dimLayer.color = new Color(0f, 0f, 0f, strength);
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
