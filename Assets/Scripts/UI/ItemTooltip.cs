using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemTooltip : MonoBehaviour
{
    public static ItemTooltip Instance { get; private set; }

    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("Cursor-Follow Positioning")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(12f, -12f);

    [Header("Anchored Positioning")]
    [SerializeField] private float aboveTargetGap = 8f;
    [SerializeField] private Vector2 anchoredOffset = Vector2.zero;

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    private bool followCursor;
    private RectTransform anchorTarget;

    private static readonly Vector3[] cornerBuffer = new Vector3[4];

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
        if (!gameObject.activeSelf) return;

        if (followCursor)
        {
            PositionAtScreenPoint((Vector2)Input.mousePosition + cursorOffset);
        }
        else if (anchorTarget != null)
        {
            PositionAboveAnchor();
        }
    }

    public void ShowTooltip(string title, string description)
    {
        if (!BeginShow(title, description)) return;

        followCursor = true;
        anchorTarget = null;

        RebuildLayoutNow();
        PositionAtScreenPoint((Vector2)Input.mousePosition + cursorOffset);
    }

    public void ShowTooltipAbove(string title, string description, RectTransform target)
    {
        if (!BeginShow(title, description)) return;

        followCursor = false;
        anchorTarget = target;

        RebuildLayoutNow();
        PositionAboveAnchor();
    }

    public void HideTooltip()
    {
        gameObject.SetActive(false);
        anchorTarget = null;
    }

    private bool BeginShow(string title, string description)
    {
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description)) return false;

        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        return true;
    }

    private void RebuildLayoutNow()
    {
        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    private void PositionAboveAnchor()
    {
        if (parentCanvas == null || rectTransform == null || anchorTarget == null) return;

        anchorTarget.GetWorldCorners(cornerBuffer);
        Vector3 topCenterWorld = (cornerBuffer[1] + cornerBuffer[2]) / 2f;

        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 topCenterScreen = RectTransformUtility.WorldToScreenPoint(cam, topCenterWorld);

        Vector2 size = rectTransform.rect.size;
        Vector2 targetScreenPos = new Vector2(topCenterScreen.x - size.x / 2f, topCenterScreen.y + aboveTargetGap + size.y);
        targetScreenPos += anchoredOffset;

        PositionAtScreenPoint(targetScreenPos);
    }

    private void PositionAtScreenPoint(Vector2 screenPos)
    {
        if (parentCanvas == null || rectTransform == null) return;

        if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 sizePixels = Vector2.Scale(rectTransform.rect.size, rectTransform.lossyScale);

            float minX = 0f;
            float maxX = Screen.width - sizePixels.x;
            float minY = sizePixels.y;
            float maxY = Screen.height;

            screenPos.x = maxX >= minX ? Mathf.Clamp(screenPos.x, minX, maxX) : minX;
            screenPos.y = maxY >= minY ? Mathf.Clamp(screenPos.y, minY, maxY) : maxY;

            rectTransform.position = screenPos;
        }
        else
        {
            RectTransform canvasRect = parentCanvas.transform as RectTransform;
            if (canvasRect == null) return;

            Camera cam = parentCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out Vector2 localPoint);

            Rect bounds = canvasRect.rect;
            Vector2 size = rectTransform.rect.size;

            float minX = bounds.xMin;
            float maxX = bounds.xMax - size.x;
            float minY = bounds.yMin + size.y;
            float maxY = bounds.yMax;

            localPoint.x = maxX >= minX ? Mathf.Clamp(localPoint.x, minX, maxX) : minX;
            localPoint.y = maxY >= minY ? Mathf.Clamp(localPoint.y, minY, maxY) : maxY;

            rectTransform.anchoredPosition = localPoint;
        }
    }
}