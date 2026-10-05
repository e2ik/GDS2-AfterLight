using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class ScrollToSelected : MonoBehaviour
{
    [SerializeField, Min(0f)] private float scrollSpeed = 15f;
    [SerializeField, Min(0f)] private float padding = 8f;
    [SerializeField] private bool ignoreWhenUsingMouse = true;

    private ScrollRect scrollRect;
    private readonly Vector3[] corners = new Vector3[4];

    private void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    private void LateUpdate()
    {
        if (scrollRect == null || scrollRect.content == null || EventSystem.current == null) return;
        if (ignoreWhenUsingMouse && InputModeTracker.IsUsingMouse) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(scrollRect.content)) return;

        RectTransform content = scrollRect.content;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)transform;
        RectTransform target = (RectTransform)selected.transform;

        target.GetWorldCorners(corners);
        float top = viewport.InverseTransformPoint(corners[1]).y + padding;
        float bottom = viewport.InverseTransformPoint(corners[0]).y - padding;
        Rect view = viewport.rect;

        float delta = 0f;
        if (top > view.yMax) delta = top - view.yMax;
        else if (bottom < view.yMin) delta = bottom - view.yMin;

        if (Mathf.Abs(delta) < 0.5f) return;

        float maxScroll = Mathf.Max(0f, content.rect.height - view.height);
        Vector2 position = content.anchoredPosition;
        float targetY = Mathf.Clamp(position.y - delta, 0f, maxScroll);

        float t = scrollSpeed <= 0f ? 1f : 1f - Mathf.Exp(-scrollSpeed * Time.unscaledDeltaTime);
        position.y = Mathf.Lerp(position.y, targetY, t);
        content.anchoredPosition = position;
        scrollRect.velocity = Vector2.zero;
    }
}