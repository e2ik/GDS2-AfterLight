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
    [Header("Animation Settings")]
    [SerializeField] private UIAnimationType animationType = UIAnimationType.SlideAndFade;
    [SerializeField] private Direction slideFrom = Direction.Right;
    [SerializeField] private float duration = 0.3f;
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool keepActiveWhenHidden = true;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 targetPosition;
    private Coroutine activeAnimation;
    private bool isInitialized = false;

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
        if (GameManager.Instance?.Player?.Controller != null && !GameManager.Instance.Player.Controller.InputEnabled)
        {
            GameManager.Instance.Player.Controller.InputEnabled = true;
        }

        EnsureInitialized();

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (activeAnimation != null)
        {
            StopCoroutine(activeAnimation);
        }

        activeAnimation = StartCoroutine(AnimateWindow(false));
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

        if (!show && !keepActiveWhenHidden)
        {
            gameObject.SetActive(false);
        }

        activeAnimation = null;
    }

    private Vector2 GetOffscreenPosition(Direction direction)
    {
        RectTransform parentRect = rectTransform.parent as RectTransform;
        float width = parentRect != null ? parentRect.rect.width : Screen.width;
        float height = parentRect != null ? parentRect.rect.height : Screen.height;

        switch (direction)
        {
            case Direction.Left:   return targetPosition + new Vector2(-width, 0);
            case Direction.Right:  return targetPosition + new Vector2(width, 0);
            case Direction.Top:    return targetPosition + new Vector2(0, height);
            case Direction.Bottom: return targetPosition + new Vector2(0, -height);
            default:               return targetPosition;
        }
    }
}