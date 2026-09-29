using System.Collections;
using UnityEngine;

public class FadeCanvasController : MonoBehaviour
{
    public static FadeCanvasController Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;

    public CanvasGroup Group => canvasGroup;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    public Coroutine FadeTo(float targetAlpha, float duration)
    {
        if (canvasGroup == null)
        {
            Debug.LogError("[FadeCanvasController] No CanvasGroup assigned/found.", this);
            return null;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
        return fadeRoutine;
    }

    public Coroutine FadeOut(float duration) => FadeTo(1f, duration);

    public Coroutine FadeIn(float duration) => FadeTo(0f, duration);

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
            fadeRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeRoutine = null;
    }
}