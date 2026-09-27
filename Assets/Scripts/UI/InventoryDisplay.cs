using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryDisplay : GameUI.UIWindow
{
    public enum InventoryFilter
    {
        All,
        GearAndWeapons,
        Primary,
        Secondary,
        Special
    }

    [System.Serializable]
    public class FilterButtonVisual
    {
        public InventoryFilter filter;
        public Graphic graphic;
    }

    [Header("UI Container")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("Filter Buttons")]
    [SerializeField] private FilterButtonVisual[] filterButtons;
    [SerializeField] private Color activeFilterColor = Color.yellow;
    [SerializeField] private Color inactiveFilterColor = Color.white;

    [Header("Controller")]
    [SerializeField] private InputActionReference prevFilterAction;
    [SerializeField] private InputActionReference nextFilterAction;

    [Header("Navigation")]
    [SerializeField] private bool wrapNavigation = true;

    [Header("Scrolling")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField, Min(0f)] private float scrollPadding = 12f;
    [SerializeField, Min(0f)] private float scrollSpeed = 18f;

    [Header("Tooltip")]
    [SerializeField] private RectTransform tooltipDock;
    [SerializeField] private TooltipAnchorSettings tooltipAnchor = new TooltipAnchorSettings
    {
        targetPoint = new Vector2(0f, 1f),
        tooltipPivot = new Vector2(0f, 1f),
        offset = Vector2.zero
    };

    [Header("Empty State")]
    [SerializeField] private TextMeshProUGUI emptyStateText;

    [Header("Lore Panel")]
    [SerializeField] private LorePanel lorePanel;
    [SerializeField] private GameObject statsAndGearPanel;

    public RectTransform TooltipDock => tooltipDock;
    public TooltipAnchorSettings TooltipAnchor => tooltipAnchor;

    private PlayerInventoryManager invManager;
    private PlayerEquipmentManager equipManager;
    private InventoryFilter currentFilter = InventoryFilter.All;
    private int rememberedSlotIndex;
    private int lastObservedSlotIndex = -1;
    private string openLoreSetID;

    private bool isScrolling;
    private float scrollTarget;
    private bool scrollToTopOnRefresh;

    private struct SlotNode
    {
        public Selectable Selectable;
        public float X;
        public float Y;
    }

    private readonly List<SlotNode> navNodes = new List<SlotNode>();
    private readonly List<List<SlotNode>> navRows = new List<List<SlotNode>>();

    private readonly struct DisplayItem
    {
        public readonly object Item;
        public readonly int PickupOrder;
        public readonly string GroupKey;
        public readonly int GroupSortOrder;
        public readonly int RarityRank;
        public readonly bool IsEquipped;

        public DisplayItem(object item, int pickupOrder, string groupKey, int groupSortOrder, int rarityRank, bool isEquipped)
        {
            Item = item;
            PickupOrder = pickupOrder;
            GroupKey = groupKey;
            GroupSortOrder = groupSortOrder;
            RarityRank = rarityRank;
            IsEquipped = isEquipped;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        if (scrollRect == null && slotContainer != null)
        {
            scrollRect = slotContainer.GetComponentInParent<ScrollRect>(true);
        }

        ApplyFilterVisuals();
    }

    private void Update()
    {
        if (!IsOpen) return;

        TrackSelection();
        UpdateScroll();
    }

    private void OnDestroy()
    {
        UnsubscribeFilterActions();

        if (invManager != null)
        {
            invManager.OnInventoryChanged -= RefreshUI;
        }

        if (equipManager != null)
        {
            equipManager.OnEquipmentChanged -= RefreshUI;
        }
    }

    public void ShowAll() => SetFilter(InventoryFilter.All);
    public void ShowGearAndWeapons() => SetFilter(InventoryFilter.GearAndWeapons);
    public void ShowPrimary() => SetFilter(InventoryFilter.Primary);
    public void ShowSecondary() => SetFilter(InventoryFilter.Secondary);
    public void ShowSpecial() => SetFilter(InventoryFilter.Special);

    public void SetFilter(InventoryFilter filter)
    {
        currentFilter = filter;
        ApplyFilterVisuals();
        scrollToTopOnRefresh = true;
        RefreshUI();
    }

    private void ApplyFilterVisuals()
    {
        if (filterButtons == null) return;

        foreach (FilterButtonVisual entry in filterButtons)
        {
            if (entry == null || entry.graphic == null) continue;
            entry.graphic.color = entry.filter == currentFilter ? activeFilterColor : inactiveFilterColor;
        }
    }

    public void RegisterInventoryManager(PlayerInventoryManager manager)
    {
        if (invManager != null)
        {
            invManager.OnInventoryChanged -= RefreshUI;
        }

        if (equipManager != null)
        {
            equipManager.OnEquipmentChanged -= RefreshUI;
        }

        invManager = manager;

        if (invManager != null)
        {
            invManager.OnInventoryChanged += RefreshUI;

            equipManager = invManager.GetComponent<PlayerEquipmentManager>();
            if (equipManager != null)
            {
                equipManager.OnEquipmentChanged += RefreshUI;
            }

            if (IsOpen)
            {
                RefreshUI();
            }
        }
    }

    protected override void OnWindowOpened()
    {
        if (prevFilterAction != null)
        {
            prevFilterAction.action.Enable();
            prevFilterAction.action.performed += HandlePrevFilter;
        }

        if (nextFilterAction != null)
        {
            nextFilterAction.action.Enable();
            nextFilterAction.action.performed += HandleNextFilter;
        }

        ApplyFilterVisuals();
        RefreshUI();

        isScrolling = false;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    protected override void OnWindowClosed()
    {
        UnsubscribeFilterActions();
        currentFilter = InventoryFilter.All;
        rememberedSlotIndex = 0;
        lastObservedSlotIndex = -1;
        isScrolling = false;

        CloseLorePanel();

        EventSystem.current?.SetSelectedGameObject(null);

        if (ItemTooltip.Instance != null)
        {
            ItemTooltip.Instance.HideTooltip();
        }
    }

    public void OnLoreSlotClicked(LoreSetDisplayInfo loreSet)
    {
        if (lorePanel == null) return;

        if (openLoreSetID == loreSet.SetID)
        {
            CloseLorePanel();
            return;
        }

        openLoreSetID = loreSet.SetID;
        if (statsAndGearPanel != null) statsAndGearPanel.SetActive(false);
        lorePanel.Show(loreSet);
    }

    public void CloseLorePanel()
    {
        if (openLoreSetID == null) return;

        openLoreSetID = null;
        if (lorePanel != null) lorePanel.Hide();
        if (statsAndGearPanel != null) statsAndGearPanel.SetActive(true);
    }

    protected override Selectable GetInitialSelectable()
    {
        if (slotContainer != null && slotContainer.childCount > 0)
        {
            Selectable first = slotContainer.GetChild(0).GetComponentInChildren<Selectable>();
            if (first != null) { return first; }
        }

        return base.GetInitialSelectable();
    }

    private void UnsubscribeFilterActions()
    {
        if (prevFilterAction != null)
        {
            prevFilterAction.action.performed -= HandlePrevFilter;
            prevFilterAction.action.Disable();
        }

        if (nextFilterAction != null)
        {
            nextFilterAction.action.performed -= HandleNextFilter;
            nextFilterAction.action.Disable();
        }
    }

    private void HandlePrevFilter(InputAction.CallbackContext context) => CycleFilter(-1);
    private void HandleNextFilter(InputAction.CallbackContext context) => CycleFilter(1);

    private void CycleFilter(int step)
    {
        if (GameUI.UIManager.Instance == null || !GameUI.UIManager.Instance.IsTopmost(this)) { return; }

        int count = System.Enum.GetValues(typeof(InventoryFilter)).Length;
        int next = ((int)currentFilter + step + count) % count;

        SetFilter((InventoryFilter)next);

        if (GetSelectedSlotIndex() < 0 && !RestoreSelectedSlot(rememberedSlotIndex))
        {
            Reselect();
        }
    }

    private void TrackSelection()
    {
        int index = GetSelectedSlotIndex();
        if (index < 0 || index == lastObservedSlotIndex) return;

        lastObservedSlotIndex = index;
        rememberedSlotIndex = index;
        ScrollToSlot(index, false);
    }

    private int GetSelectedSlotIndex()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null || selected.transform == slotContainer || !selected.transform.IsChildOf(slotContainer)) return -1;

        Transform t = selected.transform;
        while (t.parent != slotContainer) t = t.parent;
        return t.GetSiblingIndex();
    }

    private bool RestoreSelectedSlot(int index)
    {
        if (index < 0 || slotContainer.childCount == 0) return false;

        int clamped = Mathf.Min(index, slotContainer.childCount - 1);
        Selectable target = slotContainer.GetChild(clamped).GetComponentInChildren<Selectable>();
        if (target == null) return false;

        lastObservedSlotIndex = clamped;
        EventSystem.current?.SetSelectedGameObject(target.gameObject);
        ScrollToSlot(clamped, true);
        return true;
    }

    private void ScrollToSlot(int index, bool instant)
    {
        if (scrollRect == null || scrollRect.content == null || index < 0 || index >= slotContainer.childCount) return;

        RectTransform slot = slotContainer.GetChild(index) as RectTransform;
        if (slot == null) return;

        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
        RectTransform content = scrollRect.content;

        float viewHeight = viewport.rect.height;
        float scrollable = content.rect.height - viewHeight;
        if (scrollable <= 0f)
        {
            isScrolling = false;
            return;
        }

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, slot);
        float slotTop = content.rect.yMax - bounds.max.y;
        float slotBottom = content.rect.yMax - bounds.min.y;

        float currentNormalized = isScrolling ? scrollTarget : scrollRect.verticalNormalizedPosition;
        float offset = (1f - currentNormalized) * scrollable;

        if (slotTop - scrollPadding < offset) offset = slotTop - scrollPadding;
        else if (slotBottom + scrollPadding > offset + viewHeight) offset = slotBottom + scrollPadding - viewHeight;

        offset = Mathf.Clamp(offset, 0f, scrollable);
        float target = 1f - offset / scrollable;

        if (instant || scrollSpeed <= 0f)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = target;
            isScrolling = false;
        }
        else
        {
            scrollTarget = target;
            isScrolling = true;
        }
    }

    private void UpdateScroll()
    {
        if (!isScrolling || scrollRect == null) return;

        float current = scrollRect.verticalNormalizedPosition;
        float next = Mathf.Lerp(current, scrollTarget, 1f - Mathf.Exp(-scrollSpeed * Time.unscaledDeltaTime));

        if (Mathf.Abs(next - scrollTarget) < 0.0005f)
        {
            next = scrollTarget;
            isScrolling = false;
        }

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = next;
    }

    private void BuildSlotNavigation()
    {
        navNodes.Clear();
        navRows.Clear();

        for (int i = 0; i < slotContainer.childCount; i++)
        {
            Transform child = slotContainer.GetChild(i);
            Selectable selectable = child.GetComponentInChildren<Selectable>();
            if (selectable == null) continue;

            Vector3 local = slotContainer.InverseTransformPoint(child.position);
            navNodes.Add(new SlotNode { Selectable = selectable, X = local.x, Y = local.y });
        }

        if (navNodes.Count == 0) return;

        GroupIntoRows();
        ApplyNavigation();
    }

    private void GroupIntoRows()
    {
        RectTransform firstSlot = slotContainer.GetChild(0) as RectTransform;
        float rowTolerance = firstSlot != null ? Mathf.Max(1f, firstSlot.rect.height * 0.5f) : 1f;

        navNodes.Sort((a, b) => b.Y.CompareTo(a.Y));

        List<SlotNode> currentRow = null;
        float rowY = 0f;

        foreach (SlotNode node in navNodes)
        {
            if (currentRow == null || Mathf.Abs(node.Y - rowY) > rowTolerance)
            {
                currentRow = new List<SlotNode>();
                navRows.Add(currentRow);
                rowY = node.Y;
            }

            currentRow.Add(node);
        }

        foreach (List<SlotNode> row in navRows)
        {
            row.Sort((a, b) => a.X.CompareTo(b.X));
        }
    }

    private void ApplyNavigation()
    {
        for (int r = 0; r < navRows.Count; r++)
        {
            List<SlotNode> row = navRows[r];

            for (int c = 0; c < row.Count; c++)
            {
                SlotNode node = row[c];
                Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };

                Selectable left = c > 0 ? row[c - 1].Selectable : (wrapNavigation ? row[row.Count - 1].Selectable : null);
                Selectable right = c < row.Count - 1 ? row[c + 1].Selectable : (wrapNavigation ? row[0].Selectable : null);

                int upRow = r > 0 ? r - 1 : (wrapNavigation ? navRows.Count - 1 : -1);
                int downRow = r < navRows.Count - 1 ? r + 1 : (wrapNavigation ? 0 : -1);

                nav.selectOnLeft = Other(node.Selectable, left);
                nav.selectOnRight = Other(node.Selectable, right);
                nav.selectOnUp = upRow >= 0 ? Other(node.Selectable, ClosestInRow(navRows[upRow], node.X)) : null;
                nav.selectOnDown = downRow >= 0 ? Other(node.Selectable, ClosestInRow(navRows[downRow], node.X)) : null;

                node.Selectable.navigation = nav;
            }
        }
    }

    private static Selectable Other(Selectable self, Selectable candidate)
    {
        return candidate != self ? candidate : null;
    }

    private static Selectable ClosestInRow(List<SlotNode> row, float x)
    {
        Selectable best = null;
        float bestDistance = float.MaxValue;

        foreach (SlotNode node in row)
        {
            float distance = Mathf.Abs(node.X - x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = node.Selectable;
            }
        }

        return best;
    }

    private void CollectDisplayItems<T>(List<T> items, System.Func<T, int> pickupOrderSelector, bool include, List<DisplayItem> into) where T : class
    {
        if (!include || items == null) return;

        foreach (T item in items)
        {
            if (item != null) into.Add(BuildDisplayItem(item, pickupOrderSelector(item)));
        }
    }

    private void CollectLoreDisplayItems(List<LoreItemInstance> items, bool include, List<DisplayItem> into)
    {
        if (!include || items == null) return;

        Dictionary<string, LoreSetDisplayInfo> sets = new Dictionary<string, LoreSetDisplayInfo>();

        foreach (LoreItemInstance instance in items)
        {
            if (instance == null) continue;

            LoreItemDefinition def = GameDatabase.GetLoreItemTemplateFromID(instance.InstItemID);
            string setID = def != null ? def.EffectiveSetID : instance.InstItemID;

            if (!sets.TryGetValue(setID, out LoreSetDisplayInfo info))
            {
                info = new LoreSetDisplayInfo { SetID = setID, TotalPieces = def != null ? Mathf.Max(1, def.TotalPieces) : 1 };
                sets[setID] = info;
            }

            info.OwnedInstances.Add(instance);

            LoreItemDefinition repDef = info.RepresentativeInstance != null
                ? GameDatabase.GetLoreItemTemplateFromID(info.RepresentativeInstance.InstItemID)
                : null;

            if (info.RepresentativeInstance == null || def == null || (repDef != null && def.PieceIndex < repDef.PieceIndex))
            {
                info.RepresentativeInstance = instance;
            }
        }

        foreach (LoreSetDisplayInfo info in sets.Values)
        {
            LoreItemDefinition repDef = GameDatabase.GetLoreItemTemplateFromID(info.RepresentativeInstance.InstItemID);

            int earliestPickupOrder = info.OwnedInstances[0].PickupOrder;
            foreach (LoreItemInstance owned in info.OwnedInstances)
            {
                if (owned.PickupOrder < earliestPickupOrder) earliestPickupOrder = owned.PickupOrder;
            }

            into.Add(new DisplayItem(info, earliestPickupOrder, "Lore", repDef != null ? repDef.SortOrder : 0, -1, false));
        }
    }

    private DisplayItem BuildDisplayItem(object item, int pickupOrder)
    {
        switch (item)
        {
            case WeaponInstance weapon:
                {
                    WeaponDefinition def = GameDatabase.GetWeaponTemplateFromID(weapon.InstTemplateID);
                    bool equipped = equipManager != null && equipManager.IsWeaponEquipped(weapon);
                    return new DisplayItem(item, pickupOrder, "Weapon", def != null ? def.SortOrder : 0, (int)weapon.Rarity, equipped);
                }
            case GearInstance gear:
                {
                    GearDefinition def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
                    bool equipped = equipManager != null && equipManager.IsGearEquipped(gear);
                    string groupKey = def != null ? $"Slot:{def.Slot}" : "Slot:Unknown";
                    return new DisplayItem(item, pickupOrder, groupKey, def != null ? def.SortOrder : 0, (int)gear.Rarity, equipped);
                }
            case PrimaryGemInstance primary:
                {
                    PrimaryGemBehaviourDefinition def = GameDatabase.GetPrimaryTemplateFromID(primary.InstTemplateID);
                    bool equipped = equipManager != null && !string.IsNullOrEmpty(primary.InstTemplateID)
                        && primary.InstTemplateID == equipManager.GetEquippedPrimaryGemID();
                    string groupKey = $"Primary:{primary.InstTemplateID}";
                    return new DisplayItem(item, pickupOrder, groupKey, def != null ? def.SortOrder : 0, -1, equipped);
                }
            case SecondaryGemInstance secondary:
                {
                    SecondaryGemBehaviourDefinition def = GameDatabase.GetSecondaryTemplateFromID(secondary.InstTemplateID);
                    bool equipped = equipManager != null && equipManager.IsGemEquipped(secondary);
                    string groupKey = $"Secondary:{secondary.InstTemplateID}";
                    return new DisplayItem(item, pickupOrder, groupKey, def != null ? def.SortOrder : 0, (int)secondary.Rarity, equipped);
                }
            case KeyInstance key:
                {
                    KeyDefinition def = GameDatabase.GetKeyTemplateFromID(key.InstItemID);
                    return new DisplayItem(item, pickupOrder, "Key", def != null ? def.SortOrder : 0, -1, false);
                }
            case LoreItemInstance lore:
                {
                    LoreItemDefinition def = GameDatabase.GetLoreItemTemplateFromID(lore.InstItemID);
                    return new DisplayItem(item, pickupOrder, "Lore", def != null ? def.SortOrder : 0, -1, false);
                }
            default:
                return new DisplayItem(item, pickupOrder, "Unknown", 0, -1, false);
        }
    }

    private static void SortDisplayItems(List<DisplayItem> displayItems)
    {
        Dictionary<string, int> groupOrder = new Dictionary<string, int>();
        foreach (DisplayItem entry in displayItems)
        {
            if (!groupOrder.TryGetValue(entry.GroupKey, out int existing) || entry.GroupSortOrder < existing)
            {
                groupOrder[entry.GroupKey] = entry.GroupSortOrder;
            }
        }

        displayItems.Sort((a, b) =>
        {
            int equippedCompare = (b.IsEquipped ? 1 : 0).CompareTo(a.IsEquipped ? 1 : 0);
            if (equippedCompare != 0) return equippedCompare;

            int groupOrderCompare = groupOrder[a.GroupKey].CompareTo(groupOrder[b.GroupKey]);
            if (groupOrderCompare != 0) return groupOrderCompare;

            int groupKeyCompare = string.CompareOrdinal(a.GroupKey, b.GroupKey);
            if (groupKeyCompare != 0) return groupKeyCompare;

            int rarityCompare = b.RarityRank.CompareTo(a.RarityRank);
            if (rarityCompare != 0) return rarityCompare;

            return a.PickupOrder.CompareTo(b.PickupOrder);
        });
    }

    private string GetEmptyStateMessage()
    {
        return currentFilter switch
        {
            InventoryFilter.GearAndWeapons => "No Weapons or Gear found yet.",
            InventoryFilter.Primary => "No Primary Gems found yet.",
            InventoryFilter.Secondary => "No Secondary Gems found yet.",
            InventoryFilter.Special => "No Keys or Lore Fragments found yet.",
            _ => "No items found yet."
        };
    }

    private void UpdateEmptyState(bool isEmpty)
    {
        if (emptyStateText == null) return;

        emptyStateText.gameObject.SetActive(isEmpty);
        if (isEmpty) emptyStateText.text = GetEmptyStateMessage();
    }

    private static void ApplyToSlot(InventorySlot slot, object item)
    {
        switch (item)
        {
            case SecondaryGemInstance gem: slot.SetupSlot(gem); break;
            case GearInstance gear: slot.SetupSlot(gear); break;
            case PrimaryGemInstance primaryGem: slot.SetupSlot(primaryGem); break;
            case WeaponInstance weapon: slot.SetupSlot(weapon); break;
            case KeyInstance key: slot.SetupSlot(key); break;
            case LoreItemInstance loreItem: slot.SetupSlot(loreItem); break;
            case LoreSetDisplayInfo loreSet: slot.SetupSlot(loreSet); break;
        }
    }

    public void RefreshUI()
    {
        if (slotContainer == null || slotPrefab == null || invManager == null || invManager.currentInventory == null)
            return;

        TrackSelection();
        bool hadSlotSelected = GetSelectedSlotIndex() >= 0;

        PlayerInventorySO activeInventory = invManager.currentInventory;

        for (int i = slotContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = slotContainer.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        List<DisplayItem> displayItems = new List<DisplayItem>();

        bool showGearAndWeapons = currentFilter is InventoryFilter.All or InventoryFilter.GearAndWeapons;
        bool showPrimary = currentFilter is InventoryFilter.All or InventoryFilter.Primary;
        bool showSecondary = currentFilter is InventoryFilter.All or InventoryFilter.Secondary;
        bool showSpecial = currentFilter is InventoryFilter.All or InventoryFilter.Special;

        CollectDisplayItems(activeInventory.SecondaryGems, g => g.PickupOrder, showSecondary, displayItems);
        CollectDisplayItems(activeInventory.GearInstances, g => g.PickupOrder, showGearAndWeapons, displayItems);
        CollectDisplayItems(activeInventory.PrimaryGems, g => g.PickupOrder, showPrimary, displayItems);
        CollectDisplayItems(activeInventory.Weapons, w => w.PickupOrder, showGearAndWeapons, displayItems);
        CollectDisplayItems(activeInventory.KeyInstances, k => k.PickupOrder, showSpecial, displayItems);
        CollectLoreDisplayItems(activeInventory.LoreItemInstances, showSpecial, displayItems);

        SortDisplayItems(displayItems);

        UpdateEmptyState(displayItems.Count == 0);

        foreach (DisplayItem entry in displayItems)
        {
            GameObject newSlot = Instantiate(slotPrefab, slotContainer);
            if (newSlot.TryGetComponent(out InventorySlot slotScript))
            {
                ApplyToSlot(slotScript, entry.Item);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer as RectTransform);

        BuildSlotNavigation();

        if (scrollToTopOnRefresh)
        {
            scrollToTopOnRefresh = false;
            isScrolling = false;
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        if (hadSlotSelected) { RestoreSelectedSlot(rememberedSlotIndex); }
    }
}