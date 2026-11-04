using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Foundation.UI
{
    /// <summary>
    /// A list row that slides left to show a delete button behind it (drag, or hold the row). Vertical drags are
    /// handed to the surrounding <see cref="ScrollRect"/> so the list still scrolls when it starts on a row.
    /// </summary>
    public class UiSwipeRow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Button deleteButton;
        [SerializeField, Min(1f)] private float revealWidth = 64f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 0.6f;

        private ScrollRect parentScroll;
        private bool forwarding;
        private bool pressed;
        private bool dragged;
        private float pressedAt;

        public event Action DeleteRequested;

        public bool IsOpen => content != null && content.anchoredPosition.x < -revealWidth * 0.5f;

        private void Awake()
        {
            deleteButton.onClick.AddListener(() => DeleteRequested?.Invoke());
            parentScroll = GetComponentInParent<ScrollRect>();
        }

        private void OnEnable()
        {
            Close();
        }

        public void Open()
        {
            SetOffset(-revealWidth);
        }

        public void Close()
        {
            SetOffset(0f);
        }

        private void Update()
        {
            if (pressed && !dragged && Time.unscaledTime - pressedAt >= holdSeconds)
            {
                pressed = false;
                Open();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            dragged = false;
            pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragged = true;
            pressed = false;
            forwarding = Mathf.Abs(eventData.delta.y) > Mathf.Abs(eventData.delta.x) && parentScroll != null;
            if (forwarding)
            {
                ExecuteEvents.Execute(parentScroll.gameObject, eventData, ExecuteEvents.beginDragHandler);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (forwarding)
            {
                ExecuteEvents.Execute(parentScroll.gameObject, eventData, ExecuteEvents.dragHandler);
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            SetOffset(content.anchoredPosition.x + eventData.delta.x / scale);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (forwarding)
            {
                ExecuteEvents.Execute(parentScroll.gameObject, eventData, ExecuteEvents.endDragHandler);
                forwarding = false;
                return;
            }

            if (IsOpen)
            {
                Open();
            }
            else
            {
                Close();
            }
        }

        private void SetOffset(float x)
        {
            if (content == null)
            {
                return;
            }

            content.anchoredPosition = new Vector2(Mathf.Clamp(x, -revealWidth, 0f), content.anchoredPosition.y);

            // The button sits behind the row; hide it while the row covers it so its edge never shows around rounded corners.
            if (deleteButton != null)
            {
                deleteButton.gameObject.SetActive(content.anchoredPosition.x < -0.5f);
            }
        }
    }
}
