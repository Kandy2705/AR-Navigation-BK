using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>How an overlay enters and leaves.</summary>
    public enum OverlayStyle
    {
        /// <summary>Bottom sheet: the scrim fades in while the panel rises from below the screen.</summary>
        SlideUp,

        /// <summary>Centered card: fades in while growing to full size with a slight overshoot.</summary>
        Pop,

        /// <summary>Whole overlay fades.</summary>
        Fade,
    }

    /// <summary>
    /// Show / hide animation shared by the dialogs, sheets and overlays. The owner activates its GameObject and calls
    /// <see cref="Show"/>; <see cref="Hide"/> plays the exit and then runs the owner's deactivation. Outside Play
    /// mode both are instant, so EditMode tests see the final state immediately.
    /// </summary>
    public sealed class UiOverlayMotion
    {
        public const float SlideInSeconds = 0.34f;
        public const float SlideOutSeconds = 0.22f;
        public const float PopInSeconds = 0.3f;
        public const float PopOutSeconds = 0.16f;
        public const float FadeInSeconds = 0.2f;
        public const float FadeOutSeconds = 0.15f;

        /// <summary>Extra distance below the screen a sheet starts from, so its shadow and corners are hidden too.</summary>
        private const float SlideMargin = 48f;

        private const float PopStartScale = 0.86f;
        private const float PopEndScale = 0.94f;

        private readonly MonoBehaviour host;
        private readonly OverlayStyle style;
        private readonly RectTransform panel;
        private readonly Graphic scrim;

        private CanvasGroup group;
        private Vector2 panelRest;
        private bool restCaptured;
        private float progress;
        private Coroutine running;

        public UiOverlayMotion(MonoBehaviour host, OverlayStyle style, RectTransform panel = null, Graphic scrim = null)
        {
            this.host = host;
            this.style = style;
            this.panel = panel;
            this.scrim = scrim;
        }

        /// <summary>False from the moment <see cref="Hide"/> is called, even while the exit is still playing.</summary>
        public bool Visible { get; private set; }

        public void Show()
        {
            Visible = true;
            Prepare();
            if (!UiMotion.CanAnimate(host))
            {
                progress = 1f;
                Apply(true);
                return;
            }

            running = host.StartCoroutine(Animate(1f, null));
        }

        public void Hide(Action deactivate)
        {
            Visible = false;
            Prepare();
            if (!host.gameObject.activeSelf || !UiMotion.CanAnimate(host))
            {
                Finish(deactivate);
                return;
            }

            running = host.StartCoroutine(Animate(0f, deactivate));
        }

        /// <summary>Freezes the overlay at one moment of its entry or exit (0 = hidden, 1 = shown); used by preview renders.</summary>
        public void Pose(float shown, bool entering)
        {
            Visible = true;
            Prepare();
            progress = Mathf.Clamp01(shown);
            Apply(entering);
        }

        private void Prepare()
        {
            if (running != null)
            {
                if (host != null)
                {
                    host.StopCoroutine(running);
                }

                running = null;
            }

            if (group == null)
            {
                group = host.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = host.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (!restCaptured && panel != null)
            {
                panelRest = panel.anchoredPosition;
                restCaptured = true;
            }

            group.interactable = Visible;
            group.blocksRaycasts = true;
        }

        private IEnumerator Animate(float target, Action deactivate)
        {
            bool entering = target > progress;
            float from = progress;
            float full = Seconds(entering);
            float seconds = full * Mathf.Abs(target - from);
            yield return UiMotion.Run(seconds, t =>
            {
                progress = Mathf.Lerp(from, target, t);
                Apply(entering);
            });

            running = null;
            if (!entering)
            {
                Finish(deactivate);
            }
        }

        /// <summary>Puts everything back at rest (fully shown) and lets the owner deactivate.</summary>
        private void Finish(Action deactivate)
        {
            progress = 1f;
            Apply(true);
            progress = 0f;
            deactivate?.Invoke();
        }

        private float Seconds(bool entering)
        {
            switch (style)
            {
                case OverlayStyle.SlideUp:
                    return entering ? SlideInSeconds : SlideOutSeconds;
                case OverlayStyle.Pop:
                    return entering ? PopInSeconds : PopOutSeconds;
                default:
                    return entering ? FadeInSeconds : FadeOutSeconds;
            }
        }

        private void Apply(bool entering)
        {
            float p = progress;
            float fade = entering ? UiMotion.OutCubic(p) : p;
            switch (style)
            {
                case OverlayStyle.SlideUp:
                    if (scrim != null)
                    {
                        group.alpha = 1f;
                        scrim.canvasRenderer.SetAlpha(fade);
                    }
                    else
                    {
                        group.alpha = fade;
                    }

                    if (panel != null)
                    {
                        float travel = panel.rect.height + SlideMargin;
                        float hidden = entering ? 1f - UiMotion.OutQuart(p) : UiMotion.InCubic(1f - p);
                        panel.anchoredPosition = panelRest + new Vector2(0f, -travel * hidden);
                    }

                    break;

                case OverlayStyle.Pop:
                    group.alpha = fade;
                    if (panel != null)
                    {
                        float scale = entering
                            ? Mathf.LerpUnclamped(PopStartScale, 1f, UiMotion.OutBack(p))
                            : Mathf.Lerp(PopEndScale, 1f, p);
                        panel.localScale = new Vector3(scale, scale, 1f);
                    }

                    break;

                default:
                    group.alpha = fade;
                    break;
            }
        }
    }
}
