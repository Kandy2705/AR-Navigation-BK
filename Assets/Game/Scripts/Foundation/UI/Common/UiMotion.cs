using System;
using System.Collections;
using UnityEngine;

namespace Game.Foundation.UI
{
    /// <summary>How a page change is animated.</summary>
    public enum PageTransition
    {
        /// <summary>Forward: the new page slides in from the right over the old one, which drifts left and dims.</summary>
        Push,

        /// <summary>Back: the current page slides out to the right, uncovering the previous one.</summary>
        Pop,

        /// <summary>Tabs, login, logout, the end of a flow: the new page cross-fades in on top, in place.</summary>
        Fade,
    }

    /// <summary>Where a page sits at one moment of a transition.</summary>
    public struct PagePose
    {
        /// <summary>Horizontal offset in canvas units.</summary>
        public float X;
        public float Alpha;
        public float Scale;

        /// <summary>Strength of the dark layer laid over the page (0 = none).</summary>
        public float Dim;

        public static readonly PagePose Rest = new PagePose { X = 0f, Alpha = 1f, Scale = 1f, Dim = 0f };
    }

    /// <summary>
    /// The motion design of the app shell: easing curves, page transition poses and overlay timings. Poses are
    /// pure functions of normalized time, so EditMode tests and preview renders can check any frame.
    /// </summary>
    public static class UiMotion
    {
        public const float PushSeconds = 0.38f;
        public const float FadeSeconds = 0.26f;

        /// <summary>How far the page underneath travels, as a share of the screen width (parallax).</summary>
        public const float UnderlayShift = 0.3f;

        /// <summary>Darkness laid over the page underneath while another one covers it.</summary>
        public const float UnderlayDim = 0.4f;

        /// <summary>
        /// Share of a fade during which the old page stays fully opaque under the new one. Long, so nothing behind the
        /// pages shows through; the old page still fades at the end for pages that are see-through (the AR overlay).
        /// </summary>
        public const float FadeOutDelay = 0.7f;

        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float OutQuart(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u * u;
        }

        public static float InCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>Ease-out with a small overshoot, for things that pop in.</summary>
        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.4f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float Duration(PageTransition transition)
        {
            return transition == PageTransition.Fade ? FadeSeconds : PushSeconds;
        }

        /// <summary>
        /// Pose of a page at normalized time <paramref name="t"/> (0 → 1) of a transition.
        /// <paramref name="entering"/> is true for the page being shown, false for the one being hidden.
        /// </summary>
        public static PagePose Page(PageTransition transition, bool entering, float t, float width)
        {
            t = Mathf.Clamp01(t);
            PagePose pose = PagePose.Rest;
            switch (transition)
            {
                case PageTransition.Push:
                {
                    float e = OutQuart(t);
                    if (entering)
                    {
                        pose.X = width * (1f - e);
                    }
                    else
                    {
                        pose.X = -width * UnderlayShift * e;
                        pose.Dim = UnderlayDim * e;
                    }

                    break;
                }

                case PageTransition.Pop:
                {
                    float e = OutQuart(t);
                    if (entering)
                    {
                        pose.X = -width * UnderlayShift * (1f - e);
                        pose.Dim = UnderlayDim * (1f - e);
                    }
                    else
                    {
                        pose.X = width * e;
                    }

                    break;
                }

                default:
                {
                    // No scale or offset: the header and tab bar every top-level page shares stay put instead of ghosting.
                    if (entering)
                    {
                        pose.Alpha = OutCubic(t);
                    }
                    else
                    {
                        float late = Mathf.Clamp01((t - FadeOutDelay) / (1f - FadeOutDelay));
                        pose.Alpha = 1f - late;
                    }

                    break;
                }
            }

            return pose;
        }

        /// <summary>Runs <paramref name="step"/> with normalized time 0 → 1 over <paramref name="seconds"/> of real time.</summary>
        public static IEnumerator Run(float seconds, Action<float> step)
        {
            if (seconds <= 0f)
            {
                step(1f);
                yield break;
            }

            float elapsed = 0f;
            step(0f);
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(elapsed / seconds));
            }
        }

        /// <summary>Animations only run in Play mode on an active object; elsewhere (EditMode tests, builders) changes are instant.</summary>
        public static bool CanAnimate(MonoBehaviour host)
        {
            return host != null && Application.isPlaying && host.isActiveAndEnabled;
        }
    }
}
