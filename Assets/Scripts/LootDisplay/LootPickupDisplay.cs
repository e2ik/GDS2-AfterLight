using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LootPickupDisplay : MonoBehaviour
{
    public static LootPickupDisplay Instance { get; private set; }

    private readonly struct PickupData
    {
        public readonly string Name;
        public readonly string Body;

        public PickupData(string name, string body)
        {
            Name = name;
            Body = body;
        }
    }

    [Header("Setup")]
    [SerializeField] private LootPickupEntry entryPrefab;
    [SerializeField] private Transform contentParent;

    [Header("Line Cap")]
    [SerializeField] private int maxVisibleLines = 5;

    [Header("Ordering")]
    [SerializeField] private bool newestOnTop = true;

    [Header("Timing")]
    [SerializeField] private float holdDuration = 3f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private bool clearWhenWindowOpens = true;
    [SerializeField] private GameUI.UIWindow[] clearForWindows;

    [Header("Controller Inspect")]
    [SerializeField] private InputActionReference inspectAction;
    [SerializeField] private float inspectDuration = 4f;
    [SerializeField] private bool cycleBeforeClosing = true;
    [SerializeField] private GameObject inspectPrompt;

    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipDock;
    [SerializeField] private TooltipAnchorSettings inspectAnchor = new TooltipAnchorSettings();
    [SerializeField] private bool overrideTooltipWidth = true;
    [SerializeField] private float tooltipWidth = 300f;
    [SerializeField] private Graphic lootOnlyBackground;
    [SerializeField] private int lootPaddingTop = 10;
    [SerializeField] private int lootPaddingBottom = 10;
    [SerializeField] private int quickEquipPaddingTop = 50;
    [SerializeField] private int quickEquipPaddingBottom = 50;
    [SerializeField] private HorizontalOrVerticalLayoutGroup lootControlledContainer;

    [Header("Debug")]
    [SerializeField] private bool debugDrawTooltip;
    [SerializeField] private Color debugColor = Color.green;
    [SerializeField] private bool debugLogLootStyle;

    public TooltipAnchorSettings TooltipAnchor => inspectAnchor;
    public RectTransform TooltipDock => tooltipDock;

    private readonly List<LootPickupEntry> activeEntries = new List<LootPickupEntry>();
    private readonly Stack<LootPickupEntry> pool = new Stack<LootPickupEntry>();
    private readonly Dictionary<LootPickupEntry, PickupData> entryData = new Dictionary<LootPickupEntry, PickupData>();
    private VerticalLayoutGroup layoutGroup;

    private LootPickupEntry inspectedEntry;
    private float inspectTimer;
    private bool lootStyleActive;
    private bool lootHasQuickEquip;
    private LootPickupEntry lootStyleEntry;
    private LootPickupEntry pointerHoveredEntry;
    private HorizontalOrVerticalLayoutGroup controlledContainer;
    private bool originalControlWidth;
    private bool originalControlHeight;
    private LayoutGroup paddedLayout;
    private int originalPaddingTop;
    private int originalPaddingBottom;
    private RectTransform resizedTooltip;
    private Vector2 originalTooltipSize;
    private ContentSizeFitter resizedFitter;
    private ContentSizeFitter.FitMode originalHorizontalFit;
    private CanvasGroup inspectPromptGroup;
    private bool clearWindowWasOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyOrdering();
        SetLootBackground(false);

        if (inspectPrompt != null)
        {
            inspectPromptGroup = inspectPrompt.GetComponent<CanvasGroup>();
            if (inspectPromptGroup == null) inspectPromptGroup = inspectPrompt.AddComponent<CanvasGroup>();
            inspectPromptGroup.alpha = 0f;
            inspectPrompt.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        UpdateClearOnWindow();
        UpdateInspectPrompt();
    }

    private void UpdateClearOnWindow()
    {
        if (!clearWhenWindowOpens) return;

        bool open = IsClearWindowOpen();
        if (open && !clearWindowWasOpen) ClearAll();
        clearWindowWasOpen = open;
    }

    private bool IsClearWindowOpen()
    {
        if (clearForWindows != null && clearForWindows.Length > 0)
        {
            foreach (GameUI.UIWindow window in clearForWindows)
            {
                if (window != null && window.IsOpen) return true;
            }
            return false;
        }

        return GameUI.UIManager.Instance != null && GameUI.UIManager.Instance.HasOpenWindows;
    }

    public void ClearAll()
    {
        EndInspect();
        pointerHoveredEntry = null;

        for (int i = activeEntries.Count - 1; i >= 0; i--)
        {
            LootPickupEntry entry = activeEntries[i];
            activeEntries.RemoveAt(i);
            if (entry != null) ReturnToPool(entry);
        }
    }

    private void UpdateInspectPrompt()
    {
        if (inspectPrompt == null) return;

        float alpha = 0f;
        foreach (LootPickupEntry entry in activeEntries)
        {
            if (entry == null || !entry.gameObject.activeInHierarchy) continue;
            CanvasGroup group = entry.GetComponent<CanvasGroup>();
            alpha = Mathf.Max(alpha, group != null ? group.alpha : 1f);
        }

        bool visible = alpha > 0f;
        if (inspectPrompt.activeSelf != visible) inspectPrompt.SetActive(visible);
        if (inspectPromptGroup != null) inspectPromptGroup.alpha = alpha;
    }

    private void OnEnable()
    {
        if (inspectAction == null) return;

        if (!inspectAction.action.enabled) inspectAction.action.Enable();
        inspectAction.action.performed += HandleInspect;
    }

    private void OnDisable()
    {
        if (inspectAction != null)
        {
            inspectAction.action.performed -= HandleInspect;
        }

        EndInspect();
    }

    private void Update()
    {
        if (inspectedEntry != null)
        {
            inspectTimer -= Time.unscaledDeltaTime;
            bool uiOpened = GameUI.UIManager.Instance != null && GameUI.UIManager.Instance.HasOpenWindows;
            if (inspectTimer <= 0f || InputModeTracker.IsUsingMouse || uiOpened)
            {
                EndInspect();
            }
        }

        if (pointerHoveredEntry != null && !pointerHoveredEntry.gameObject.activeInHierarchy) pointerHoveredEntry = null;

        LootPickupEntry target = inspectedEntry != null
            ? inspectedEntry
            : pointerHoveredEntry != null ? pointerHoveredEntry : GetHoveredEntry();

        if (target != lootStyleEntry || (target != null) != lootStyleActive)
        {
            if (debugLogLootStyle)
                Debug.Log($"[LootPickupDisplay] Loot style target -> {(target != null ? target.name : "none")} (inspected: {inspectedEntry != null}, tooltip active: {ItemTooltip.Instance != null && ItemTooltip.Instance.gameObject.activeInHierarchy})", this);

            lootStyleEntry = target;

            if (target != null)
            {
                TooltipActions actions = target.BuildActions();
                lootHasQuickEquip = actions != null && actions.EquipAvailable;
                ApplyTooltipScale();
            }
            else
            {
                RestoreTooltipScale();
            }
        }
    }

    private LootPickupEntry GetHoveredEntry()
    {
        if (Mouse.current == null || ItemTooltip.Instance == null || !ItemTooltip.Instance.gameObject.activeInHierarchy) return null;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        foreach (LootPickupEntry entry in activeEntries)
        {
            if (entry == null || !entry.gameObject.activeInHierarchy) continue;

            RectTransform rect = (RectTransform)entry.transform;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;

            if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePosition, canvasCamera))
                return entry;
        }

        return null;
    }

    private void ApplyOrdering()
    {
        if (layoutGroup == null && contentParent != null)
        {
            layoutGroup = contentParent.GetComponent<VerticalLayoutGroup>();
        }

        if (layoutGroup == null)
        {
            Debug.LogWarning("[LootPickupDisplay] contentParent has no VerticalLayoutGroup — can't control stack order.");
            return;
        }

        layoutGroup.reverseArrangement = newestOnTop;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyOrdering();
    }
#endif

    public void AddPickup(Sprite icon, string itemName, ERarity? rarity, string tooltipBody, object item = null)
    {
        if (entryPrefab == null || contentParent == null)
        {
            Debug.LogWarning("[LootPickupDisplay] Missing entryPrefab or contentParent reference.");
            return;
        }

        LootPickupEntry entry = GetPooledEntry();
        entry.transform.SetParent(contentParent, false);
        entry.transform.SetAsLastSibling();
        entry.Setup(this, icon, itemName, rarity, tooltipBody, holdDuration, fadeDuration, item);

        entryData[entry] = new PickupData(itemName, tooltipBody);
        activeEntries.Insert(0, entry);

        while (activeEntries.Count > maxVisibleLines)
        {
            LootPickupEntry oldest = activeEntries[activeEntries.Count - 1];
            activeEntries.RemoveAt(activeEntries.Count - 1);
            ReturnToPool(oldest);
        }
    }

    public void ReleaseEntry(LootPickupEntry entry)
    {
        if (activeEntries.Remove(entry))
        {
            ReturnToPool(entry);
        }
    }

    private void HandleInspect(InputAction.CallbackContext context)
    {
        if (activeEntries.Count == 0) return;
        if (GameUI.UIManager.Instance != null && GameUI.UIManager.Instance.IsInputLocked) return;

        int current = inspectedEntry != null ? activeEntries.IndexOf(inspectedEntry) : -1;

        if (inspectedEntry != null && (!cycleBeforeClosing || current >= activeEntries.Count - 1))
        {
            EndInspect();
            return;
        }

        ShowInspect(activeEntries[current + 1]);
    }

    private void ShowInspect(LootPickupEntry entry)
    {
        if (ItemTooltip.Instance == null) return;
        if (!entryData.TryGetValue(entry, out PickupData data)) return;

        if (inspectedEntry != null && inspectedEntry != entry)
        {
            inspectedEntry.SetInspected(false);
        }

        inspectedEntry = entry;
        inspectTimer = inspectDuration;
        entry.SetInspected(true);

        InputModeTracker.ForceNonMouse();
        ItemTooltip.Instance.ShowTooltipAnchored(data.Name, entry.GetTooltipBody(), (RectTransform)entry.transform, inspectAnchor, tooltipDock, entry.BuildActions());
        ApplyTooltipScale();
    }

    private void SetLootBackground(bool visible)
    {
        if (debugLogLootStyle)
            Debug.Log($"[LootPickupDisplay] Background {(visible ? "ON" : "OFF")} on '{(lootOnlyBackground != null ? lootOnlyBackground.name : "NOT ASSIGNED")}'", this);

        if (lootOnlyBackground == null) return;

        SetLootPadding(visible);
        SetLootChildControl(visible);

        Color color = lootOnlyBackground.color;
        color.a = visible ? 1f : 0f;
        lootOnlyBackground.color = color;
    }

    private void SetLootChildControl(bool loot)
    {
        if (controlledContainer == null)
        {
            controlledContainer = lootControlledContainer;
            if (controlledContainer == null && ItemTooltip.Instance != null)
                controlledContainer = ItemTooltip.Instance.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (controlledContainer == null) return;

            originalControlWidth = controlledContainer.childControlWidth;
            originalControlHeight = controlledContainer.childControlHeight;
        }

        controlledContainer.childControlWidth = loot || originalControlWidth;
        controlledContainer.childControlHeight = loot || originalControlHeight;
        if (!loot)
        {
            controlledContainer.childControlWidth = originalControlWidth;
            controlledContainer.childControlHeight = originalControlHeight;
        }
    }

    private void SetLootPadding(bool loot)
    {
        if (paddedLayout == null)
        {
            paddedLayout = lootOnlyBackground.GetComponent<LayoutGroup>();
            if (paddedLayout == null) return;

            originalPaddingTop = paddedLayout.padding.top;
            originalPaddingBottom = paddedLayout.padding.bottom;
        }

        int top = lootHasQuickEquip ? quickEquipPaddingTop : lootPaddingTop;
        int bottom = lootHasQuickEquip ? quickEquipPaddingBottom : lootPaddingBottom;

        paddedLayout.padding.top = loot ? top : originalPaddingTop;
        paddedLayout.padding.bottom = loot ? bottom : originalPaddingBottom;

        RectTransform layoutRect = (RectTransform)paddedLayout.transform;
        LayoutRebuilder.MarkLayoutForRebuild(layoutRect);
        if (ItemTooltip.Instance != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)ItemTooltip.Instance.transform);
    }

    public void SetHoveredEntry(LootPickupEntry entry)
    {
        pointerHoveredEntry = entry;
    }

    public void ClearHoveredEntry(LootPickupEntry entry)
    {
        if (pointerHoveredEntry == entry) pointerHoveredEntry = null;
    }

    public void ApplyTooltipScale()
    {
        lootStyleActive = true;
        SetLootBackground(true);

        if (!overrideTooltipWidth || ItemTooltip.Instance == null) return;

        RectTransform tooltip = ItemTooltip.Instance.transform as RectTransform;
        if (tooltip == null) return;

        if (resizedTooltip != tooltip)
        {
            RestoreTooltipScale();
            resizedTooltip = tooltip;
            originalTooltipSize = tooltip.sizeDelta;

            resizedFitter = tooltip.GetComponent<ContentSizeFitter>();
            if (resizedFitter != null) originalHorizontalFit = resizedFitter.horizontalFit;
        }

        if (resizedFitter != null) resizedFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        tooltip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, tooltipWidth);

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip);
    }

    public void RestoreTooltipScale()
    {
        lootStyleActive = false;
        SetLootBackground(false);

        if (resizedTooltip == null) return;

        if (resizedFitter != null) resizedFitter.horizontalFit = originalHorizontalFit;

        resizedTooltip.sizeDelta = new Vector2(originalTooltipSize.x, resizedTooltip.sizeDelta.y);
        LayoutRebuilder.ForceRebuildLayoutImmediate(resizedTooltip);

        resizedTooltip = null;
        resizedFitter = null;
    }

    private void EndInspect()
    {
        if (inspectedEntry == null) return;

        if (ItemTooltip.Instance != null)
        {
            ItemTooltip.Instance.HideTooltip((RectTransform)inspectedEntry.transform);
        }

        RestoreTooltipScale();

        inspectedEntry.SetInspected(false);
        inspectedEntry = null;
    }

    private Texture2D debugTexture;
    private readonly Vector3[] debugCorners = new Vector3[4];

    private void OnGUI()
    {
        if (!debugDrawTooltip || ItemTooltip.Instance == null) return;
        if (!ItemTooltip.Instance.gameObject.activeInHierarchy) return;

        RectTransform rect = ItemTooltip.Instance.transform as RectTransform;
        if (rect == null) return;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera
            : null;

        rect.GetWorldCorners(debugCorners);

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvasCamera, debugCorners[i]);
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }

        Rect guiRect = new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);

        if (debugTexture == null)
        {
            debugTexture = new Texture2D(1, 1);
            debugTexture.SetPixel(0, 0, Color.white);
            debugTexture.Apply();
        }

        Color previous = GUI.color;
        GUI.color = debugColor;

        GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMin, guiRect.width, 2f), debugTexture);
        GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMax - 2f, guiRect.width, 2f), debugTexture);
        GUI.DrawTexture(new Rect(guiRect.xMin, guiRect.yMin, 2f, guiRect.height), debugTexture);
        GUI.DrawTexture(new Rect(guiRect.xMax - 2f, guiRect.yMin, 2f, guiRect.height), debugTexture);

        LootPickupEntry hovered = GetHoveredEntry();
        float bgAlpha = lootOnlyBackground != null ? lootOnlyBackground.color.a : -1f;
        string info = $"Rect {rect.rect.width:0}x{rect.rect.height:0}  Screen {guiRect.width:0}x{guiRect.height:0}px  {(lootStyleActive ? "LOOT" : "NORMAL")}  Hover: {(hovered != null ? hovered.name : "none")}  BG alpha: {bgAlpha:0.##}";
        GUI.Label(new Rect(guiRect.xMin, guiRect.yMin - 20f, 600f, 20f), info);

        GUI.color = previous;
    }

    private LootPickupEntry GetPooledEntry()
    {
        if (pool.Count > 0)
        {
            LootPickupEntry pooled = pool.Pop();
            pooled.gameObject.SetActive(true);
            return pooled;
        }

        return Instantiate(entryPrefab);
    }

    private void ReturnToPool(LootPickupEntry entry)
    {
        if (entry == inspectedEntry) EndInspect();

        entryData.Remove(entry);
        entry.gameObject.SetActive(false);
        entry.transform.SetParent(transform, false);
        pool.Push(entry);
    }
}