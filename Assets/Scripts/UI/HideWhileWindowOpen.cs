using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class HideWhileWindowOpen : MonoBehaviour
{
    [SerializeField] private GameUI.UIWindow[] onlyForWindows;
    [SerializeField, Min(0f)] private float fadeDuration = 0.15f;
    [SerializeField] private bool blockRaycastsWhileHidden = true;
    [SerializeField] private bool hideDuringDialogue = true;

    private CanvasGroup canvasGroup;
    private bool originalBlocksRaycasts;
    private bool originalInteractable;
    private float visibility = 1f;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        originalBlocksRaycasts = canvasGroup.blocksRaycasts;
        originalInteractable = canvasGroup.interactable;
    }

    private void OnDisable()
    {
        visibility = 1f;
        Apply();
    }

    private void LateUpdate()
    {
        float target = ShouldHide() ? 0f : 1f;

        if (fadeDuration <= 0f) visibility = target;
        else visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime / fadeDuration);

        Apply();
    }

    private bool ShouldHide()
    {
        if (hideDuringDialogue && DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return true;

        if (onlyForWindows != null && onlyForWindows.Length > 0)
        {
            foreach (GameUI.UIWindow window in onlyForWindows)
            {
                if (window != null && window.IsOpen) return true;
            }
            return false;
        }

        return GameUI.UIManager.Instance != null && GameUI.UIManager.Instance.HasOpenWindows;
    }

    private void Apply()
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visibility;

        bool hidden = visibility < 1f;
        canvasGroup.blocksRaycasts = hidden && blockRaycastsWhileHidden ? false : originalBlocksRaycasts;
        canvasGroup.interactable = hidden ? false : originalInteractable;
    }
}