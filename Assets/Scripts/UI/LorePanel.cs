using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LorePanel : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Complete State")]
    [SerializeField] private GameObject completeGroup;
    [SerializeField] private Image fullImage;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Locked State")]
    [SerializeField] private GameObject lockedGroup;
    [SerializeField] private TextMeshProUGUI lockedText;

    [Header("Scrolling")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private InputActionReference scrollAction;
    [SerializeField] private float scrollSpeed = 600f;
    [SerializeField, Range(0f, 1f)] private float stickDeadzone = 0.2f;

    // private UIWindowAnimator animator;

    // private void Awake()
    // {
    //     animator = GetComponent<UIWindowAnimator>();
    //     if (animator != null) animator.InstantHide();
    // }

    private void OnEnable()
    {
        if (scrollAction != null && !scrollAction.action.enabled) scrollAction.action.Enable();
    }

    private void Update()
    {
        if (scrollRect == null) scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (scrollRect == null || scrollRect.content == null) return;

        float input = ReadScrollInput();
        if (Mathf.Abs(input) < stickDeadzone) return;

        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
        float scrollable = scrollRect.content.rect.height - viewport.rect.height;
        if (scrollable <= 0f) return;

        float delta = input * scrollSpeed * Time.unscaledDeltaTime / scrollable;
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + delta);
    }

    private float ReadScrollInput()
    {
        if (scrollAction != null)
        {
            InputAction action = scrollAction.action;
            if (action.expectedControlType == "Vector2") return action.ReadValue<Vector2>().y;
            return action.ReadValue<float>();
        }

        return Gamepad.current != null ? Gamepad.current.rightStick.ReadValue().y : 0f;
    }

    public void Show(LoreSetDisplayInfo loreSet)
    {
        LoreItemDefinition def = loreSet.RepresentativeInstance != null
            ? GameDatabase.GetLoreItemTemplateFromID(loreSet.RepresentativeInstance.InstItemID)
            : null;

        if (titleText != null) titleText.text = def != null ? def.UIName : "";

        bool complete = loreSet.IsComplete;
        if (completeGroup != null) completeGroup.SetActive(complete);
        if (lockedGroup != null) lockedGroup.SetActive(!complete);

        if (complete && def != null)
        {
            if (fullImage != null)
            {
                fullImage.sprite = def.FullImage;
                fullImage.enabled = def.FullImage != null;
            }

            if (bodyText != null) bodyText.text = def.ItemText;
        }
        else if (lockedText != null)
        {
            lockedText.text = $"{loreSet.OwnedCount}/{loreSet.TotalPieces} pages found.\nFind the rest to read this.";
        }

        // if (animator != null) animator.Show();
        // else gameObject.SetActive(true);
        gameObject.SetActive(true);

        if (scrollRect == null) scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void Hide()
    {
        // if (animator != null) animator.Hide();
        // else gameObject.SetActive(false);
        gameObject.SetActive(false);
    }
}