using UnityEngine;
using TMPro;

public class ItemTooltip : MonoBehaviour
{
    public static ItemTooltip Instance { get; private set; }

    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Positioning")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(12f, -12f);

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (rectTransform != null)
        {
            rectTransform.pivot = new Vector2(0f, 1f);
        }

        HideTooltip();
    }

    private void Update()
    {
        if (gameObject.activeSelf)
        {
            FollowMouse();
        }
    }

    public void ShowTooltip(string title, string description)
    {
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description)) return;

        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;

        gameObject.SetActive(true);
        FollowMouse();
    }

    public void HideTooltip()
    {
        gameObject.SetActive(false);
    }

    private void FollowMouse()
    {
        if (parentCanvas == null || rectTransform == null) return;

        Vector2 mousePos = Input.mousePosition;

        if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            rectTransform.position = mousePos + cursorOffset;
        }
        else
        {
            // ScreenSpace - Camera or World Space conversion
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentCanvas.transform as RectTransform,
                mousePos,
                parentCanvas.worldCamera,
                out Vector2 localPoint
            );

            rectTransform.anchoredPosition = localPoint + cursorOffset;
        }
    }
}