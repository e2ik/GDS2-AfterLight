using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollRectSelectionFollow : MonoBehaviour
    {
        private ScrollRect scrollRect;
        private GameObject lastSelected;

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
        }

        private void Update()
        {
            if (EventSystem.current == null) return;

            GameObject current = EventSystem.current.currentSelectedGameObject;
            if (current == lastSelected) return;
            lastSelected = current;

            if (current == null || scrollRect.content == null) return;
            if (!current.transform.IsChildOf(scrollRect.content)) return;

            if (current.TryGetComponent(out RectTransform targetRect))
            {
                ScrollToShow(targetRect);
            }
        }

        private void ScrollToShow(RectTransform target)
        {
            if (scrollRect.viewport == null) return;

            Canvas.ForceUpdateCanvases();

            RectTransform content = scrollRect.content;
            RectTransform viewport = scrollRect.viewport;

            float viewportHeight = viewport.rect.height;
            float contentHeight = content.rect.height;
            if (contentHeight <= viewportHeight) return;

            Vector2 targetLocalPos = content.InverseTransformPoint(target.position);
            float targetTop = content.rect.height * (1f - content.pivot.y) - (targetLocalPos.y + target.rect.height * (1f - target.pivot.y));
            float targetBottom = targetTop + target.rect.height;

            float currentTop = (1f - scrollRect.verticalNormalizedPosition) * (contentHeight - viewportHeight);
            float currentBottom = currentTop + viewportHeight;

            float newTop = currentTop;
            if (targetTop < currentTop) newTop = targetTop;
            else if (targetBottom > currentBottom) newTop = targetBottom - viewportHeight;

            scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(newTop / (contentHeight - viewportHeight));
        }
    }
}