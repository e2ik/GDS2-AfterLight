using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button actionButton;
    [SerializeField] private Image borderImage;

    [Header("Text Settings")]
    [SerializeField] private bool showText = true;

    [Header("Equipped Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color equippedColor = Color.green;
    [SerializeField] private bool colorButtonByRarity = true;

    [Header("Border")]
    [SerializeField] private Color noRarityBorderColor = Color.white;

    [Header("Selection")]
    [SerializeField] private Graphic selectionGraphic;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool hideWhenUnselected = true;
    [SerializeField] private bool pulseWhenSelected = true;
    [SerializeField, Min(1f)] private float pulseScale = 1.2f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 1.5f;

    [Header("Tooltip")]
    [SerializeField, Min(0f)] private float controllerTooltipDelay = 0.5f;

    [Header("New Item Badge")]
    [SerializeField] private GameObject newBadge;
    [SerializeField] private bool pulseNewBadge = true;
    [SerializeField, Min(0.01f)] private float badgePulseSpeed = 1.5f;
    [SerializeField, Range(0f, 1f)] private float badgeMinOpacity = 0.4f;

    [Header("Click Sounds")]
    [SerializeField] private FMODUnity.EventReference loreClickSound;
    [SerializeField] private FMODUnity.EventReference keyClickSound;

    [Header("Lore Set Progress")]
    [SerializeField] private TextMeshProUGUI progressText;

    private object currentItem;
    public object CurrentItem => currentItem;
    private InventoryDisplay cachedInventoryDisplay;
    private UIWindowAnimator windowAnimator;
    private RectTransform rectTransform;
    private bool isSelected;
    private bool isPointerOver;
    private bool tooltipWanted;
    private Vector3 selectionBaseScale = Vector3.one;
    private float pulseTime;
    private CanvasGroup badgeGroup;

    private readonly struct SlotContext
    {
        public readonly Sprite Sprite;
        public readonly string Name;
        public readonly bool IsEquipped;
        public readonly ERarity? Rarity;

        public SlotContext(Sprite sprite, string name, bool isEquipped, ERarity? rarity)
        {
            Sprite = sprite;
            Name = name;
            IsEquipped = isEquipped;
            Rarity = rarity;
        }
    }

    private InventoryDisplay Display
    {
        get
        {
            if (cachedInventoryDisplay == null) cachedInventoryDisplay = GetComponentInParent<InventoryDisplay>(true);
            return cachedInventoryDisplay;
        }
    }

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        windowAnimator = GetComponentInParent<UIWindowAnimator>(true);

        if (iconImage != null) iconImage.raycastTarget = false;
        if (nameText != null) nameText.raycastTarget = false;
        if (selectionGraphic != null)
        {
            selectionGraphic.raycastTarget = false;
            selectionBaseScale = selectionGraphic.rectTransform.localScale;
        }
        if (progressText != null) progressText.raycastTarget = false;

        if (newBadge != null)
        {
            badgeGroup = newBadge.GetComponent<CanvasGroup>();
            if (badgeGroup == null) badgeGroup = newBadge.AddComponent<CanvasGroup>();
            badgeGroup.interactable = false;
            badgeGroup.blocksRaycasts = false;
        }

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnSlotClicked);
        }

        ApplySelectionVisual();
    }

    private void Update()
    {
        GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool selected = current != null && current.transform.IsChildOf(transform);

        if (selected != isSelected)
        {
            isSelected = selected;
            ApplySelectionVisual();
            if (isSelected) Display?.OnSlotFocused(currentItem);
        }

        UpdateSelectionPulse();
        UpdateBadgePulse();

        UpdateTooltipState();
        UpdateNewBadgeIfLookedAt();
    }

    private void OnDestroy()
    {
        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(OnSlotClicked);
        }
    }

    private void OnDisable()
    {
        isSelected = false;
        isPointerOver = false;
        tooltipWanted = false;
        ApplySelectionVisual();
    }

    public void SetupSlot(object item)
    {
        currentItem = item;

        SlotContext? ctx = BuildContext();
        if (ctx == null)
        {
            ClearDisplay();
            return;
        }

        SetSlotDisplay(ctx.Value.Sprite, ctx.Value.Name);
        SetBorderColor(ctx.Value.Rarity);
        UpdateEquippedVisuals(ctx.Value.IsEquipped, ctx.Value.Rarity);
        SetNewBadgeVisible(GetIsNew(currentItem));
        UpdateProgressText();
    }

    public void SetTextVisibility(bool visible)
    {
        showText = visible;
        if (nameText != null) nameText.gameObject.SetActive(showText);
    }

    private SlotContext? BuildContext()
    {
        InventoryItemBase def = ItemActionFactory.GetDefinition(currentItem);
        if (def == null) return null;

        PlayerEquipmentManager equip = ItemActionFactory.GetEquipment();
        bool isEquipped = ItemActionFactory.IsItemEquipped(currentItem, equip);

        return new SlotContext(def.UISprite, def.UIName, isEquipped, GetRarity(currentItem));
    }

    private static ERarity? GetRarity(object item)
    {
        return item switch
        {
            SecondaryGemInstance gem => (ERarity?)gem.Rarity,
            GearInstance gear => (ERarity?)gear.Rarity,
            WeaponInstance weapon => (ERarity?)weapon.Rarity,
            _ => null
        };
    }

    private static bool GetIsNew(object item)
    {
        return item switch
        {
            SecondaryGemInstance gem => gem.IsNew,
            GearInstance gear => gear.IsNew,
            PrimaryGemInstance primary => primary.IsNew,
            WeaponInstance weapon => weapon.IsNew,
            KeyInstance key => key.IsNew,
            LoreItemInstance lore => lore.IsNew,
            LoreSetDisplayInfo loreSet => loreSet.OwnedInstances.Exists(i => i.IsNew),
            _ => false
        };
    }

    private static bool ClearIsNew(object item)
    {
        switch (item)
        {
            case SecondaryGemInstance gem when gem.IsNew: gem.IsNew = false; return true;
            case GearInstance gear when gear.IsNew: gear.IsNew = false; return true;
            case PrimaryGemInstance primary when primary.IsNew: primary.IsNew = false; return true;
            case WeaponInstance weapon when weapon.IsNew: weapon.IsNew = false; return true;
            case KeyInstance key when key.IsNew: key.IsNew = false; return true;
            case LoreItemInstance lore when lore.IsNew: lore.IsNew = false; return true;
            case LoreSetDisplayInfo loreSet:
                {
                    bool anyCleared = false;
                    foreach (LoreItemInstance piece in loreSet.OwnedInstances)
                    {
                        if (piece.IsNew) { piece.IsNew = false; anyCleared = true; }
                    }
                    return anyCleared;
                }
            default: return false;
        }
    }

    private void SetNewBadgeVisible(bool visible)
    {
        if (newBadge == null) return;

        newBadge.SetActive(visible);
        if (!visible) ResetBadgePulse();
    }

    private void UpdateBadgePulse()
    {
        if (!pulseNewBadge || newBadge == null || !newBadge.activeSelf) return;

        float t = (1f - Mathf.Cos(Time.unscaledTime * badgePulseSpeed * Mathf.PI * 2f)) * 0.5f;
        if (badgeGroup != null) badgeGroup.alpha = Mathf.Lerp(badgeMinOpacity, 1f, t);
    }

    private void ResetBadgePulse()
    {
        if (badgeGroup != null) badgeGroup.alpha = 1f;
    }

    private void UpdateProgressText()
    {
        if (progressText == null) return;

        if (currentItem is LoreSetDisplayInfo loreSet && !loreSet.IsComplete)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = $"{loreSet.OwnedCount}/{loreSet.TotalPieces}";
        }
        else
        {
            progressText.gameObject.SetActive(false);
        }
    }

    private void UpdateNewBadgeIfLookedAt()
    {
        bool lookedAt = InputModeTracker.IsUsingMouse ? isPointerOver : isSelected;
        if (!lookedAt) return;

        if (ClearIsNew(currentItem))
        {
            SetNewBadgeVisible(false);
        }
    }

    private void SetSlotDisplay(Sprite sprite, string title)
    {
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = (sprite != null);
            if (sprite != null) iconImage.color = Color.white;
        }

        if (nameText != null)
        {
            nameText.gameObject.SetActive(showText);
            nameText.text = showText ? title : "";
        }
    }

    private void SetBorderColor(ERarity? rarity)
    {
        if (borderImage == null) return;

        borderImage.color = GetSlotColor(rarity);
    }

    private Color GetSlotColor(ERarity? rarity)
    {
        return currentItem switch
        {
            KeyInstance when GameManager.Instance != null => GameManager.Instance.KeyItemColor,
            LoreItemInstance when GameManager.Instance != null => GameManager.Instance.LoreItemColor,
            LoreSetDisplayInfo when GameManager.Instance != null => GameManager.Instance.LoreItemColor,
            _ when rarity.HasValue && GameManager.Instance != null => GameManager.Instance.GetRarityColor(rarity.Value),
            _ => noRarityBorderColor
        };
    }

    private void ApplySelectionVisual()
    {
        if (selectionGraphic == null) return;
        selectionGraphic.color = isSelected ? selectedColor : unselectedColor;
        selectionGraphic.enabled = isSelected || !hideWhenUnselected;

        pulseTime = 0f;
        selectionGraphic.rectTransform.localScale = selectionBaseScale;
    }

    private void UpdateSelectionPulse()
    {
        if (selectionGraphic == null || !pulseWhenSelected || !isSelected) return;

        pulseTime += Time.unscaledDeltaTime * pulseSpeed;
        float t = (1f - Mathf.Cos(pulseTime * Mathf.PI * 2f)) * 0.5f;
        selectionGraphic.rectTransform.localScale = selectionBaseScale * Mathf.Lerp(1f, pulseScale, t);
    }

    private void ClearDisplay()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (nameText != null) nameText.text = "";

        if (actionButton != null && actionButton.image != null)
        {
            actionButton.image.color = normalColor;
        }

        if (borderImage != null)
        {
            borderImage.color = noRarityBorderColor;
        }

        SetNewBadgeVisible(false);
    }

    private void OnSlotClicked()
    {
        InventoryDisplay display = Display;

        if (currentItem is LoreSetDisplayInfo loreSet)
        {
            PlayClickSound(loreClickSound);
            display?.OnLoreSlotClicked(loreSet);
            UpdateTooltipState();
            return;
        }

        if (currentItem is LoreItemInstance loreItem)
        {
            PlayClickSound(loreClickSound);
            display?.OnLoreItemClicked(loreItem);
            UpdateTooltipState();
            return;
        }

        display?.CloseLorePanel();

        if (currentItem is KeyInstance) PlayClickSound(keyClickSound);

        ItemActionFactory.ToggleEquip(currentItem, ItemActionFactory.GetEquipment());

        if (display != null) display.RefreshUI();
    }

    private void PlayClickSound(FMODUnity.EventReference sound)
    {
        if (sound.IsNull) return;

        GameUI.UISFXWatcher.SuppressSubmitSound();
        AudioManager.PlaySFX(sound, this);
    }

    private void UpdateEquippedVisuals(bool isEquipped, ERarity? rarity)
    {
        if (actionButton == null || actionButton.image == null) return;

        Color targetColor = isEquipped
            ? equippedColor
            : colorButtonByRarity ? GetSlotColor(rarity) : normalColor;

        actionButton.image.color = targetColor;

        ColorBlock cb = actionButton.colors;
        Color tint = actionButton.transition == Selectable.Transition.ColorTint ? Color.white : targetColor;
        cb.normalColor = tint;
        cb.selectedColor = tint;
        actionButton.colors = cb;
    }

    #region Tooltip

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        Display?.OnSlotFocused(currentItem);
        UpdateTooltipState();
        UpdateNewBadgeIfLookedAt();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        UpdateTooltipState();
    }

    private void UpdateTooltipState()
    {
        bool windowReady = windowAnimator == null
            || (!windowAnimator.IsAnimating && windowAnimator.SecondsSinceSettled >= controllerTooltipDelay);

        bool wanted = InputModeTracker.IsUsingMouse ? isPointerOver : (isSelected && windowReady);
        
        if (Display != null && Display.IsLorePanelOpen) wanted = false;
        if (wanted == tooltipWanted) return;

        tooltipWanted = wanted;

        if (wanted) TriggerTooltip();
        else if (ItemTooltip.Instance != null) ItemTooltip.Instance.HideTooltip(rectTransform);
    }

    private void TriggerTooltip()
    {
        if (ItemTooltip.Instance == null) return;

        SlotContext? ctx = BuildContext();
        if (ctx == null)
        {
            ItemTooltip.Instance.HideTooltip(rectTransform);
            return;
        }

        InventoryDisplay display = Display;
        RectTransform dock = display != null ? display.TooltipDock : null;
        TooltipAnchorSettings settings = display != null ? display.TooltipAnchor : null;
        TooltipActions actions = ItemActionFactory.ForInventory(currentItem);
        string body = ItemActionFactory.BuildTooltipBody(currentItem);

        ItemTooltip.Instance.ShowTooltipAnchored(ctx.Value.Name, body, rectTransform, settings, dock, actions);
    }

    public void RefreshTooltip()
    {
        UpdateTooltipState();
    }

    #endregion
}