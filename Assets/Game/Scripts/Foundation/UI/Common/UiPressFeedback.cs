using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// Shrinks a button slightly while it is held and springs it back on release. Added at run time by
    /// <see cref="UiPage"/> to every button that shows a press state; full-screen scrims (no transition) are skipped.
    /// </summary>
    [DisallowMultipleComponent]
    public class UiPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float PressedScale = 0.96f;
        private const float DownSeconds = 0.08f;
        private const float UpSeconds = 0.24f;

        private Selectable selectable;
        private Vector3 restScale = Vector3.one;
        private float factor = 1f;
        private Coroutine running;
        private bool pressed;

        /// <summary>Adds the feedback to the buttons under <paramref name="root"/> that do not have it yet.</summary>
        public static void AttachTo(Transform root)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.transition == Selectable.Transition.None || button.GetComponent<UiPressFeedback>() != null)
                {
                    continue;
                }

                button.gameObject.AddComponent<UiPressFeedback>();
            }
        }

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            restScale = transform.localScale;
        }

        private void OnDisable()
        {
            running = null;
            pressed = false;
            SetFactor(1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.IsInteractable())
            {
                return;
            }

            pressed = true;
            Play(PressedScale, DownSeconds, UiMotion.OutCubic);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (!pressed)
            {
                return;
            }

            pressed = false;
            Play(1f, UpSeconds, UiMotion.OutBack);
        }

        private void Play(float target, float seconds, System.Func<float, float> ease)
        {
            if (!UiMotion.CanAnimate(this))
            {
                SetFactor(target);
                return;
            }

            if (running != null)
            {
                StopCoroutine(running);
            }

            running = StartCoroutine(ScaleTo(target, seconds, ease));
        }

        private IEnumerator ScaleTo(float target, float seconds, System.Func<float, float> ease)
        {
            float from = factor;
            yield return UiMotion.Run(seconds, t => SetFactor(Mathf.LerpUnclamped(from, target, ease(t))));
            running = null;
        }

        private void SetFactor(float value)
        {
            factor = value;
            transform.localScale = new Vector3(restScale.x * value, restScale.y * value, restScale.z);
        }
    }
}
