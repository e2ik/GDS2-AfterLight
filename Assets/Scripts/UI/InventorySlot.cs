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

    [Header("Border")]
    [SerializeField] private Color noRarityBorderColor = Color.white;

    [Header("Selection")]
    [SerializeField] private Graphic selectionGraphic;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0f);

    [Header("Tooltip")]
    [SerializeField, Min(0f)] private float controllerTooltipDelay = 0.5f;

    private object currentItem;
    private InventoryDisplay cachedInventoryDisplay;
    private UIWindowAnimator windowAnimator;
    private RectTransform rectTransform;
    private bool isSelected;
    private bool isPointerOver;
    private bool tooltipWanted;

    private readonly struct SlotContext
    {
        public readonly Sprite Sprite;
        public readonly string Name;
        public readonly System.Func<string> TooltipBody;
        public readonly bool IsEquipped;
        public readonly System.Action ToggleEquip;
        public readonly ERarity? Rarity;

        public SlotContext(Sprite sprite, string name, System.Func<string> tooltipBody, bool isEquipped, System.Action toggleEquip, ERarity? rarity)
        {
            Sprite = sprite;
            Name = name;
            TooltipBody = tooltipBody;
            IsEquipped = isEquipped;
            ToggleEquip = toggleEquip;
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
        if (selectionGraphic != null) selectionGraphic.raycastTarget = false;

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
        }

        UpdateTooltipState();
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

    public void SetupSlot(SecondaryGemInstance gem) => SetCurrentItem(gem);
    public void SetupSlot(GearInstance gear) => SetCurrentItem(gear);
    public void SetupSlot(PrimaryGemInstance gem) => SetCurrentItem(gem);
    public void SetupSlot(WeaponInstance weapon) => SetCurrentItem(weapon);

    private void SetCurrentItem(object item)
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
        UpdateEquippedVisuals(ctx.Value.IsEquipped);
    }

    public void SetTextVisibility(bool visible)
    {
        showText = visible;
        if (nameText != null) nameText.gameObject.SetActive(showText);
    }

    private static PlayerEquipmentManager GetEquipment()
    {
        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        return player != null ? player.Equipment : null;
    }

    private SlotContext? BuildContext()
    {
        PlayerEquipmentManager equip = GetEquipment();

        switch (currentItem)
        {
            case SecondaryGemInstance gem when !string.IsNullOrEmpty(gem.InstTemplateID):
            {
                var def = GameDatabase.GetSecondaryTemplateFromID(gem.InstTemplateID);
                if (def == null) return null;

                bool isEquipped = equip != null && equip.IsGemEquipped(gem);
                System.Action toggle = () =>
                {
                    if (equip == null) return;
                    if (equip.IsGemEquipped(gem)) equip.ClearSecondaryGem();
                    else equip.EquipSecondaryGem(gem);
                };

                SecondaryGemInstance equippedGemForCompare = (!isEquipped && equip != null && !equip.IsSecondaryGemSlotEmpty()) ? equip.SecondaryGem : null;
                return new SlotContext(def.UISprite, def.UIName, () => ItemTooltipTextBuilder.BuildSecondaryGemTooltip(gem, equippedGemForCompare), isEquipped, toggle, gem.Rarity);
            }

            case GearInstance gear when !string.IsNullOrEmpty(gear.InstTemplateID):
            {
                var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
                if (def == null) return null;

                bool isEquipped = equip != null && equip.IsGearEquipped(gear);
                System.Action toggle = () =>
                {
                    if (equip == null) return;
                    if (equip.IsGearEquipped(gear)) equip.ClearGear(def.Slot);
                    else equip.EquipGear(def.Slot, gear);
                };

                GearInstance equippedGearForCompare = (!isEquipped && equip != null) ? equip.GetEquippedGear(def.Slot) : null;
                return new SlotContext(def.UISprite, def.UIName, () => ItemTooltipTextBuilder.BuildGearTooltip(gear, def.Slot.ToString(), equippedGearForCompare), isEquipped, toggle, gear.Rarity);
            }

            case PrimaryGemInstance primary when !string.IsNullOrEmpty(primary.InstTemplateID):
            {
                var def = GameDatabase.GetPrimaryTemplateFromID(primary.InstTemplateID);
                if (def == null) return null;

                bool isEquipped = equip != null && equip.SpecialAttackDef == def;
                System.Action toggle = () =>
                {
                    if (equip == null) return;
                    if (equip.SpecialAttackDef == def) equip.ClearSpecialAttack();
                    else equip.EquipSpecialAttack(def);
                };

                return new SlotContext(def.UISprite, def.UIName, () => ItemTooltipTextBuilder.BuildPrimaryGemTooltip(def), isEquipped, toggle, null);
            }

            case WeaponInstance weapon when !string.IsNullOrEmpty(weapon.InstTemplateID):
            {
                var def = GameDatabase.GetWeaponTemplateFromID(weapon.InstTemplateID);
                if (def == null) return null;

                bool isEquipped = equip != null && equip.IsWeaponEquipped(weapon);

                System.Action toggle = () =>
                {
                    if (equip == null) return;
                    if (!equip.IsWeaponEquipped(weapon)) equip.EquipWeapon(weapon);
                };

                WeaponInstance equippedWeaponForCompare = (!isEquipped && equip != null) ? equip.EquippedWeapon : null;
                return new SlotContext(def.UISprite, def.UIName, () => ItemTooltipTextBuilder.BuildWeaponTooltip(weapon, equippedWeaponForCompare), isEquipped, toggle, weapon.Rarity);
            }

            default:
                return null;
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

        borderImage.color = rarity.HasValue && GameManager.Instance != null
            ? GameManager.Instance.GetRarityColor(rarity.Value)
            : noRarityBorderColor;
    }

    private void ApplySelectionVisual()
    {
        if (selectionGraphic == null) return;
        selectionGraphic.color = isSelected ? selectedColor : unselectedColor;
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
    }

    private void OnSlotClicked()
    {
        SlotContext? ctx = BuildContext();
        ctx?.ToggleEquip?.Invoke();

        InventoryDisplay display = Display;
        if (display != null) display.RefreshUI();
    }

    private void UpdateEquippedVisuals(bool isEquipped)
    {
        if (actionButton == null || actionButton.image == null) return;

        Color targetColor = isEquipped ? equippedColor : normalColor;
        actionButton.image.color = targetColor;

        ColorBlock cb = actionButton.colors;
        cb.normalColor = targetColor;
        cb.selectedColor = targetColor;
        actionButton.colors = cb;
    }

    #region Tooltip

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        UpdateTooltipState();
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

        ItemTooltip.Instance.ShowTooltipAnchored(ctx.Value.Name, ctx.Value.TooltipBody(), rectTransform, settings, dock);
    }

    #endregion
}