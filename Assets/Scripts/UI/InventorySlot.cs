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

    private object currentItem;
    private InventoryDisplay cachedInventoryDisplay;

    private readonly struct SlotContext
    {
        public readonly Sprite Sprite;
        public readonly string Name;
        public readonly string TooltipBody;
        public readonly bool IsEquipped;
        public readonly System.Action ToggleEquip;
        public readonly ERarity? Rarity;

        public SlotContext(Sprite sprite, string name, string tooltipBody, bool isEquipped, System.Action toggleEquip, ERarity? rarity)
        {
            Sprite = sprite;
            Name = name;
            TooltipBody = tooltipBody;
            IsEquipped = isEquipped;
            ToggleEquip = toggleEquip;
            Rarity = rarity;
        }
    }

    private void Awake()
    {
        if (iconImage != null) iconImage.raycastTarget = false;
        if (nameText != null) nameText.raycastTarget = false;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(OnSlotClicked);
        }
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
        if (ItemTooltip.Instance != null)
        {
            ItemTooltip.Instance.HideTooltip();
        }
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
        UpdateEquippedVisuals();
    }

    public void SetTextVisibility(bool visible)
    {
        showText = visible;
        if (nameText != null) nameText.gameObject.SetActive(showText);
    }

    private SlotContext? BuildContext()
    {
        Player player = Object.FindFirstObjectByType<Player>();
        PlayerEquipmentManager equip = player != null ? player.Equipment : null;

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
                return new SlotContext(def.UISprite, def.UIName, ItemTooltipTextBuilder.BuildSecondaryGemTooltip(gem, equippedGemForCompare), isEquipped, toggle, gem.Rarity);
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
                return new SlotContext(def.UISprite, def.UIName, ItemTooltipTextBuilder.BuildGearTooltip(gear, def.Slot.ToString(), equippedGearForCompare), isEquipped, toggle, gear.Rarity);
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

                // Primary gems don't roll rarity — border stays default.
                return new SlotContext(def.UISprite, def.UIName, ItemTooltipTextBuilder.BuildPrimaryGemTooltip(def), isEquipped, toggle, null);
            }

            case WeaponInstance weapon when !string.IsNullOrEmpty(weapon.InstTemplateID):
            {
                var def = GameDatabase.GetWeaponTemplateFromID(weapon.InstTemplateID);
                if (def == null) return null;

                bool isEquipped = equip != null && equip.IsWeaponEquipped(weapon);

                // note DO NOT EVER unequip the weapon lol — toggle only equips, never clears
                System.Action toggle = () =>
                {
                    if (equip == null) return;
                    if (!equip.IsWeaponEquipped(weapon)) equip.EquipWeapon(weapon);
                };

                WeaponInstance equippedWeaponForCompare = (!isEquipped && equip != null) ? equip.EquippedWeapon : null;
                return new SlotContext(def.UISprite, def.UIName, ItemTooltipTextBuilder.BuildWeaponTooltip(weapon, equippedWeaponForCompare), isEquipped, toggle, weapon.Rarity);
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

        if (cachedInventoryDisplay == null)
            cachedInventoryDisplay = Object.FindFirstObjectByType<InventoryDisplay>();

        if (cachedInventoryDisplay != null)
            cachedInventoryDisplay.RefreshUI();

        TriggerTooltip();
    }

    private void UpdateEquippedVisuals()
    {
        if (actionButton == null || actionButton.image == null) return;

        SlotContext? ctx = BuildContext();
        bool isEquipped = ctx?.IsEquipped ?? false;

        Color targetColor = isEquipped ? equippedColor : normalColor;
        actionButton.image.color = targetColor;

        ColorBlock cb = actionButton.colors;
        cb.normalColor = targetColor;
        cb.selectedColor = targetColor;
        actionButton.colors = cb;
    }

    #region Tooltip Interfaces

    public void OnPointerEnter(PointerEventData eventData)
    {
        TriggerTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemTooltip.Instance != null)
        {
            ItemTooltip.Instance.HideTooltip();
        }
    }

    private void TriggerTooltip()
    {
        if (ItemTooltip.Instance == null) return;

        SlotContext? ctx = BuildContext();
        if (ctx == null)
        {
            ItemTooltip.Instance.HideTooltip();
            return;
        }

        ItemTooltip.Instance.ShowTooltip(ctx.Value.Name, ctx.Value.TooltipBody);
    }

    #endregion
}