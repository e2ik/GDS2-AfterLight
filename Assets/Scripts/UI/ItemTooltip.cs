using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
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

    [Header("Loot Panel (Quick Equip)")]
    [FormerlySerializedAs("equipPrompt")]
    [SerializeField] private GameObject lootPanel;
    [FormerlySerializedAs("equipAction")]
    [SerializeField] private InputActionReference quickEquipAction;

    [Header("Inventory Panel (Equip Hint + Delete)")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject inventoryEquipPrompt;
    [SerializeField] private GameObject deletePrompt;
    [SerializeField] private InputActionReference deleteAction;
    [SerializeField] private Image deleteProgressFill;
    [SerializeField, Min(0.1f)] private float deleteHoldDuration = 1f;

    private enum Placement
    {
        Cursor,
        Above,
        Anchored
    }

    private static readonly Vector2 TopLeftPivot = new Vector2(0f, 1f);

    private RectTransform rectTransform;
    private Canvas parentCanvas;

    private Placement placement;
    private RectTransform anchorTarget;
    private TooltipAnchorSettings anchorSettings;
    private RectTransform owner;
    private bool hasOwner;
    private bool hidePending;

    private TooltipActions currentActions;
    private bool quickEquipEnabled;
    private bool deleteEnabled;
    private float holdTimer;
    private bool deleteLatched;

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
            rectTransform.pivot = TopLeftPivot;
        }

        HideTooltip();
    }

    private void OnEnable()
    {
        EnsureEnabled(quickEquipAction);
        EnsureEnabled(deleteAction);
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        if (hasOwner && (owner == null || !owner.gameObject.activeInHierarchy))
        {
            HideTooltip();
            return;
        }

        EnsureEnabled(quickEquipAction);
        EnsureEnabled(deleteAction);

        if (RefreshPanels()) RebuildLayoutNow();
        if (HandleActionInput()) return;

        Reposition();
    }

    private void LateUpdate()
    {
        if (hidePending) HideTooltip();
    }

    public void ShowTooltip(string title, string description)
    {
        Present(title, description, Placement.Cursor, null, null, null, null);
    }

    public void ShowTooltipAbove(string title, string description, RectTransform target)
    {
        Present(title, description, Placement.Above, null, target, null, null);
    }

    public void ShowTooltipAnchored(string title, string description, RectTransform tooltipOwner, TooltipAnchorSettings settings, RectTransform anchorRect = null, TooltipActions actions = null)
    {
        if (tooltipOwner == null) return;

        Present(title, description, Placement.Anchored, tooltipOwner, anchorRect != null ? anchorRect : tooltipOwner, settings, actions);
    }

    public void HideTooltip()
    {
        gameObject.SetActive(false);
        anchorTarget = null;
        anchorSettings = null;
        owner = null;
        hasOwner = false;
        hidePending = false;

        currentActions = null;
        quickEquipEnabled = false;
        deleteEnabled = false;
        holdTimer = 0f;
        SetDeleteProgress(0f);
    }

    public void HideTooltip(RectTransform requester)
    {
        if (requester == null || requester != owner) return;
        hidePending = true;
    }

    private void Present(string title, string description, Placement newPlacement, RectTransform newOwner, RectTransform target, TooltipAnchorSettings settings, TooltipActions actions)
    {
        if (!BeginShow(title, description)) return;

        hidePending = false;
        placement = newPlacement;
        owner = newOwner;
        hasOwner = newOwner != null;
        anchorTarget = target;
        anchorSettings = settings;
        currentActions = actions;

        holdTimer = 0f;
        SetDeleteProgress(0f);
        if (deleteAction != null && deleteAction.action.IsPressed()) deleteLatched = true;

        RefreshPanels();
        RebuildLayoutNow();
        Reposition();
    }

    private bool BeginShow(string title, string description)
    {
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description)) return false;

        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;

        Transform parent = transform.parent;
        if (parent != null && transform.GetSiblingIndex() != parent.childCount - 1)
        {
            transform.SetAsLastSibling();
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);
        return true;
    }

    private static void EnsureEnabled(InputActionReference reference)
    {
        if (reference != null && !reference.action.enabled) reference.action.Enable();
    }

    private static bool IsUIOpen()
    {
        return GameUI.UIManager.Instance != null && GameUI.UIManager.Instance.IsInputLocked;
    }

    private bool RefreshPanels()
    {
        TooltipPanel panel = currentActions != null ? currentActions.Panel : TooltipPanel.None;

        bool showLoot = panel == TooltipPanel.Loot && currentActions.EquipAvailable && !IsUIOpen();
        bool showInventory = panel == TooltipPanel.Inventory;
        bool showEquipHint = showInventory && currentActions.EquipHintAvailable;
        bool showDelete = showInventory && currentActions.DeleteAvailable;

        quickEquipEnabled = showLoot;
        deleteEnabled = showDelete;

        bool changed = false;
        changed |= SetActiveIfChanged(lootPanel, showLoot);
        changed |= SetActiveIfChanged(inventoryPanel, showInventory);
        changed |= SetActiveIfChanged(inventoryEquipPrompt, showEquipHint);
        changed |= SetActiveIfChanged(deletePrompt, showDelete);
        return changed;
    }

    private static bool SetActiveIfChanged(GameObject target, bool active)
    {
        if (target == null || target.activeSelf == active) return false;

        target.SetActive(active);
        return true;
    }

    private bool HandleActionInput()
    {
        if (currentActions == null) return false;

        if (quickEquipEnabled && quickEquipAction != null && quickEquipAction.action.WasPressedThisFrame())
        {
            TooltipActions actions = currentActions;
            HideTooltip();
            actions.Equip();
            return true;
        }

        bool pressed = deleteAction != null && deleteAction.action.IsPressed();
        if (!pressed) deleteLatched = false;

        if (deleteEnabled && pressed && !deleteLatched)
        {
            holdTimer += Time.unscaledDeltaTime;
            SetDeleteProgress(holdTimer / deleteHoldDuration);

            if (holdTimer >= deleteHoldDuration)
            {
                TooltipActions actions = currentActions;
                HideTooltip();
                deleteLatched = true;
                actions.Delete();
                return true;
            }
        }
        else if (holdTimer > 0f)
        {
            holdTimer = 0f;
            SetDeleteProgress(0f);
        }

        return false;
    }

    private void SetDeleteProgress(float value)
    {
        if (deleteProgressFill != null) deleteProgressFill.fillAmount = Mathf.Clamp01(value);
    }

    private void RebuildLayoutNow()
    {
        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    private void Reposition()
    {
        switch (placement)
        {
            case Placement.Cursor:
                PositionAtScreenPoint((Vector2)Input.mousePosition + cursorOffset);
                break;
            case Placement.Above:
                PositionAboveAnchor();
                break;
            default:
                PositionAtAnchor();
                break;
        }
    }

    private void PositionAtAnchor()
    {
        if (anchorSettings != null) PositionWithSettings(anchorSettings);
        else PositionAboveAnchor();
    }

    private void PositionWithSettings(TooltipAnchorSettings s)
    {
        if (parentCanvas == null || rectTransform == null || anchorTarget == null) return;

        anchorTarget.GetWorldCorners(cornerBuffer);
        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, cornerBuffer[2]);

        Vector2 point = new Vector2(
            Mathf.Lerp(min.x, max.x, s.targetPoint.x),
            Mathf.Lerp(min.y, max.y, s.targetPoint.y));

        point += s.offset * parentCanvas.scaleFactor;

        PositionAtScreenPoint(point, s.tooltipPivot);
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
        PositionAtScreenPoint(screenPos, TopLeftPivot);
    }

    private void PositionAtScreenPoint(Vector2 screenPos, Vector2 pivot)
    {
        if (parentCanvas == null || rectTransform == null) return;

        if (rectTransform.pivot != pivot) rectTransform.pivot = pivot;

        if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 sizePixels = Vector2.Scale(rectTransform.rect.size, rectTransform.lossyScale);

            float minX = pivot.x * sizePixels.x;
            float maxX = Screen.width - (1f - pivot.x) * sizePixels.x;
            float minY = pivot.y * sizePixels.y;
            float maxY = Screen.height - (1f - pivot.y) * sizePixels.y;

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

            float minX = bounds.xMin + pivot.x * size.x;
            float maxX = bounds.xMax - (1f - pivot.x) * size.x;
            float minY = bounds.yMin + pivot.y * size.y;
            float maxY = bounds.yMax - (1f - pivot.y) * size.y;

            localPoint.x = maxX >= minX ? Mathf.Clamp(localPoint.x, minX, maxX) : minX;
            localPoint.y = maxY >= minY ? Mathf.Clamp(localPoint.y, minY, maxY) : maxY;

            rectTransform.anchoredPosition = localPoint;
        }
    }
}