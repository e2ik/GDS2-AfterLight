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

    [Header("Text Settings")]
    [SerializeField] private bool showText = true;

    [Header("Equipped Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color equippedColor = Color.green;

    private object currentItem;
    private InventoryDisplay cachedInventoryDisplay;

    private readonly struct SlotContext
    {
        public readonly Sprite Sprite;
        public readonly string Name;
        public readonly string TooltipBody;
        public readonly bool IsEquipped;
        public readonly System.Action ToggleEquip;

        public SlotContext(Sprite sprite, string name, string tooltipBody, bool isEquipped, System.Action toggleEquip)
        {
            Sprite = sprite;
            Name = name;
            TooltipBody = tooltipBody;
            IsEquipped = isEquipped;
            ToggleEquip = toggleEquip;
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

                return new SlotContext(def.UISprite, def.UIName, GetGemStatsTooltip(gem), isEquipped, toggle);
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

                return new SlotContext(def.UISprite, def.UIName, GetGearStatsTooltip(gear, def.Slot.ToString()), isEquipped, toggle);
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

                return new SlotContext(def.UISprite, def.UIName, def.GemAttackDescription, isEquipped, toggle);
            }

            case WeaponInstance weapon when !string.IsNullOrEmpty(weapon.InstTemplateID):
            {
                var def = GameDatabase.GetWeaponTemplateFromID(weapon.InstTemplateID);
                if (def == null) return null;

                bool isEquipped = equip != null && equip.IsWeaponEquipped(weapon);
                string stats = $"Damage: {weapon.InstRolledDamage:F1}\nRange: {weapon.InstRolledRange:F1}\nCrit: {weapon.InstRolledCrit * 100f:F1}%";

                // note DO NOT EVER unequip the weapon lol — toggle only equips, never clears
                System.Action toggle = () =>
                {
                    if (equip == null) return;
                    if (!equip.IsWeaponEquipped(weapon)) equip.EquipWeapon(weapon);
                };

                return new SlotContext(def.UISprite, def.UIName, stats, isEquipped, toggle);
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

    private string GetGearStatsTooltip(GearInstance gear, string slotName)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Slot: {slotName}");

        int attack = (int)gear.InstBonusAttack;
        int defense = (int)gear.InstBonusDefense;
        int humanity = (int)gear.InstBonusHumanity;
        float crit = gear.InstBonusCrit;

        if (attack > 0) sb.AppendLine($"Attack: +{attack}");
        if (defense > 0) sb.AppendLine($"Defense: +{defense}");
        if (humanity > 0) sb.AppendLine($"Humanity: +{humanity}");
        if (crit > 0) sb.AppendLine($"Crit: +{crit * 100f:F1}%");

        return sb.ToString().TrimEnd();
    }

    private string GetGemStatsTooltip(SecondaryGemInstance gem)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("Type: Secondary Gem");

        int damageBonus = gem.InstRolledDamageValue;
        int critBonus = gem.InstRolledCritValue;
        int dotPercent = gem.InstRolledDotPercent;

        if (damageBonus > 0) sb.AppendLine($"Bonus Damage: +{damageBonus}");
        if (critBonus > 0) sb.AppendLine($"Bonus Crit: +{critBonus}%");
        if (dotPercent > 0) sb.AppendLine($"Bleed: {dotPercent}% of hit damage over time");

        return sb.ToString().TrimEnd();
    }

    #endregion
}