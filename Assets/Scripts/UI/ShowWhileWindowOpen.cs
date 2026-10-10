using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ShowWhileWindowOpen : MonoBehaviour
{
    [SerializeField] private GameUI.UIWindow[] windows;
    [SerializeField, Min(0f)] private float fadeDuration = 0.15f;

    private CanvasGroup canvasGroup;
    private bool originalBlocksRaycasts;
    private float visibility;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        originalBlocksRaycasts = canvasGroup.blocksRaycasts;
        visibility = ShouldShow() ? 1f : 0f;
        Apply();
    }

    private void LateUpdate()
    {
        float target = ShouldShow() ? 1f : 0f;

        if (fadeDuration <= 0f) visibility = target;
        else visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime / fadeDuration);

        Apply();
    }

    private bool ShouldShow()
    {
        if (windows == null) return false;

        foreach (GameUI.UIWindow window in windows)
        {
            if (window != null && window.IsOpen) return true;
        }

        return false;
    }

    private void Apply()
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visibility;
        canvasGroup.blocksRaycasts = visibility > 0f && originalBlocksRaycasts;
        canvasGroup.interactable = false;
    }
}