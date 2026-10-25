using System;
using System.Collections;
using UnityEngine;

public enum UIAnimationType
{
    Fade,
    Slide,
    SlideAndFade
}

public enum Direction
{
    Left,
    Right,
    Top,
    Bottom
}

[RequireComponent(typeof(CanvasGroup))]
public class UIWindowAnimator : MonoBehaviour
{
    public static event Action<UIWindowAnimator> OnAnyShown;

    [Header("Animation Settings")]
    [SerializeField] private UIAnimationType animationType = UIAnimationType.SlideAndFade;
    [SerializeField] private Direction slideFrom = Direction.Right;
    [SerializeField] private float duration = 0.3f;
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool keepActiveWhenHidden = true;
    [SerializeField, Min(0f)] private float slideDistanceOverride = 0f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 targetPosition;
    private Coroutine activeAnimation;
    private bool isInitialized = false;
    private float settledAtTime = float.NegativeInfinity;

    public bool IsAnimating => isActiveAndEnabled && activeAnimation != null && duration > 0f;
    public float SecondsSinceSettled => IsAnimating ? 0f : Time.unscaledTime - settledAtTime;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;

        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        targetPosition = rectTransform.anchoredPosition;
        isInitialized = true;
    }

    public void InstantHide()
    {
        EnsureInitialized();

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        bool useSlide = animationType == UIAnimationType.Slide || animationType == UIAnimationType.SlideAndFade;
        bool useFade = animationType == UIAnimationType.Fade || animationType == UIAnimationType.SlideAndFade;

        rectTransform.anchoredPosition = useSlide ? GetOffscreenPosition(slideFrom) : targetPosition;
        canvasGroup.alpha = useFade ? 0f : 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (!keepActiveWhenHidden)
        {
            gameObject.SetActive(false);
        }
    }

    public void Show(bool freezeplayer = false)
    {
        OnAnyShown?.Invoke(this);

        if (freezeplayer && GameManager.Instance?.Player?.Controller != null)
        {
            GameManager.Instance.Player.Controller.InputEnabled = false;
        }

        EnsureInitialized();
        gameObject.SetActive(true);

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        activeAnimation = StartCoroutine(AnimateWindow(true));
    }

    public void Hide()
    {
        EnsureInitialized();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        activeAnimation = StartCoroutine(AnimateWindow(false));
    }

    public void SetInteractable(bool interactable)
    {
        EnsureInitialized();
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    private IEnumerator AnimateWindow(bool show)
    {
        float time = 0f;

        bool useSlide = animationType == UIAnimationType.Slide || animationType == UIAnimationType.SlideAndFade;
        bool useFade = animationType == UIAnimationType.Fade || animationType == UIAnimationType.SlideAndFade;

        Vector2 startPos = show ? (useSlide ? GetOffscreenPosition(slideFrom) : targetPosition) : rectTransform.anchoredPosition;
        Vector2 endPos = show ? targetPosition : (useSlide ? GetOffscreenPosition(slideFrom) : targetPosition);

        float startAlpha = show ? (useFade ? 0f : 1f) : canvasGroup.alpha;
        float endAlpha = show ? 1f : (useFade ? 0f : 1f);

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = easeCurve.Evaluate(time / duration);

            if (useSlide)
            {
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            }

            if (useFade)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            }

            yield return null;
        }

        rectTransform.anchoredPosition = endPos;
        canvasGroup.alpha = endAlpha;

        if (!show)
        {
            var controller = GameManager.Instance?.Player?.Controller;
            if (controller != null) { controller.InputEnabled = true; }

            if (!keepActiveWhenHidden) { gameObject.SetActive(false); }
        }

        settledAtTime = Time.unscaledTime;
        activeAnimation = null;
    }

    private Vector2 GetOffscreenPosition(Direction direction)
    {
        RectTransform parentRect = rectTransform.parent as RectTransform;
        float fallbackWidth = parentRect != null ? parentRect.rect.width : Screen.width;
        float fallbackHeight = parentRect != null ? parentRect.rect.height : Screen.height;

        float width = slideDistanceOverride > 0f ? slideDistanceOverride : fallbackWidth;
        float height = slideDistanceOverride > 0f ? slideDistanceOverride : fallbackHeight;

        float required = DistanceToClearCanvas(direction, parentRect);
        width = Mathf.Max(width, required);
        height = Mathf.Max(height, required);

        switch (direction)
        {
            case Direction.Left: return targetPosition + new Vector2(-width, 0);
            case Direction.Right: return targetPosition + new Vector2(width, 0);
            case Direction.Top: return targetPosition + new Vector2(0, height);
            case Direction.Bottom: return targetPosition + new Vector2(0, -height);
            default: return targetPosition;
        }
    }

    private float DistanceToClearCanvas(Direction direction, RectTransform parentRect)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || parentRect == null) return 0f;

        RectTransform root = canvas.rootCanvas.transform as RectTransform;
        if (root == null) return 0f;

        Vector3[] panel = new Vector3[4];
        Vector3[] screen = new Vector3[4];
        rectTransform.GetWorldCorners(panel);
        root.GetWorldCorners(screen);

        Vector3 toTarget = parentRect.TransformVector(targetPosition - rectTransform.anchoredPosition);
        float panelMinX = Mathf.Min(panel[0].x, panel[2].x) + toTarget.x;
        float panelMaxX = Mathf.Max(panel[0].x, panel[2].x) + toTarget.x;
        float panelMinY = Mathf.Min(panel[0].y, panel[2].y) + toTarget.y;
        float panelMaxY = Mathf.Max(panel[0].y, panel[2].y) + toTarget.y;
        float screenMinX = Mathf.Min(screen[0].x, screen[2].x);
        float screenMaxX = Mathf.Max(screen[0].x, screen[2].x);
        float screenMinY = Mathf.Min(screen[0].y, screen[2].y);
        float screenMaxY = Mathf.Max(screen[0].y, screen[2].y);

        Vector3 worldShift;
        switch (direction)
        {
            case Direction.Left: worldShift = new Vector3(panelMaxX - screenMinX, 0f, 0f); break;
            case Direction.Right: worldShift = new Vector3(screenMaxX - panelMinX, 0f, 0f); break;
            case Direction.Top: worldShift = new Vector3(0f, screenMaxY - panelMinY, 0f); break;
            case Direction.Bottom: worldShift = new Vector3(0f, panelMaxY - screenMinY, 0f); break;
            default: return 0f;
        }

        Vector3 localShift = parentRect.InverseTransformVector(worldShift);
        float distance = direction == Direction.Left || direction == Direction.Right ? Mathf.Abs(localShift.x) : Mathf.Abs(localShift.y);
        return distance + 4f;
    }
}