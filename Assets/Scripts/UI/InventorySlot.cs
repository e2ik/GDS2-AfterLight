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

    private SecondaryGemInstance currentSecondaryGem;
    private GearInstance currentGear;
    private InventoryDisplay cachedInventoryDisplay;

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

    public void SetupSlot(SecondaryGemInstance gem)
    {
        currentSecondaryGem = gem;
        currentGear = null;

        if (gem == null || string.IsNullOrEmpty(gem.InstTemplateID))
        {
            ClearDisplay();
            return;
        }

        var def = GameDatabase.GetSecondaryTemplateFromID(gem.InstTemplateID);
        if (def != null)
        {
            SetSlotDisplay(def.UISprite, def.UIName);
        }
        else
        {
            ClearDisplay();
        }

        UpdateEquippedVisuals();
    }

    public void SetupSlot(GearInstance gear)
    {
        currentGear = gear;
        currentSecondaryGem = null;

        if (gear == null || string.IsNullOrEmpty(gear.InstTemplateID))
        {
            ClearDisplay();
            return;
        }

        var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
        if (def != null)
        {
            SetSlotDisplay(def.UISprite, def.UIName);
        }
        else
        {
            ClearDisplay();
        }

        UpdateEquippedVisuals();
    }

    public void SetTextVisibility(bool visible)
    {
        showText = visible;
        if (nameText != null) nameText.gameObject.SetActive(showText);
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
        Player player = Object.FindFirstObjectByType<Player>();
        if (player == null || player.Equipment == null) return;

        PlayerEquipmentManager equipManager = player.Equipment;

        if (currentSecondaryGem != null)
        {
            if (equipManager.IsGemEquipped(currentSecondaryGem))
                equipManager.ClearSecondaryGem();
            else
                equipManager.EquipSecondaryGem(currentSecondaryGem);
        }
        else if (currentGear != null)
        {
            var def = GameDatabase.GetGearTemplateFromID(currentGear.InstTemplateID);
            if (def != null)
            {
                if (equipManager.IsGearEquipped(currentGear))
                    equipManager.ClearGear(def.Slot);
                else
                    equipManager.EquipGear(def.Slot, currentGear);
            }
        }

        if (cachedInventoryDisplay == null)
            cachedInventoryDisplay = Object.FindFirstObjectByType<InventoryDisplay>();

        if (cachedInventoryDisplay != null)
            cachedInventoryDisplay.RefreshUI();

        TriggerTooltip();
    }

    private void UpdateEquippedVisuals()
    {
        if (actionButton == null || actionButton.image == null) return;

        Player player = Object.FindFirstObjectByType<Player>();
        bool isEquipped = false;

        if (player != null && player.Equipment != null)
        {
            if (currentSecondaryGem != null)
                isEquipped = player.Equipment.IsGemEquipped(currentSecondaryGem);
            else if (currentGear != null)
                isEquipped = player.Equipment.IsGearEquipped(currentGear);
        }

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

        if (currentSecondaryGem != null)
        {
            var def = GameDatabase.GetSecondaryTemplateFromID(currentSecondaryGem.InstTemplateID);
            if (def != null)
            {
                string stats = GetGemStatsTooltip(currentSecondaryGem);
                ItemTooltip.Instance.ShowTooltip(def.UIName, stats);
            }
        }
        else if (currentGear != null)
        {
            var def = GameDatabase.GetGearTemplateFromID(currentGear.InstTemplateID);
            if (def != null)
            {
                string stats = GetGearStatsTooltip(currentGear, def.Slot.ToString());
                ItemTooltip.Instance.ShowTooltip(def.UIName, stats);
            }
        }
        else
        {
            ItemTooltip.Instance.HideTooltip();
        }
    }

    private string GetGearStatsTooltip(GearInstance gear, string slotName)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Slot: {slotName}");

        int attack = (int)gear.InstBonusAttack;
        int defense = (int)gear.InstBonusDefense;
        int humanity = (int)gear.InstBonusHumanity;

        if (attack > 0) sb.AppendLine($"Attack: +{attack}");
        if (defense > 0) sb.AppendLine($"Defense: +{defense}");
        if (humanity > 0) sb.AppendLine($"Humanity: +{humanity}");

        return sb.ToString().TrimEnd();
    }

    private string GetGemStatsTooltip(SecondaryGemInstance gem)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("Type: Secondary Gem");

        int damageMult = (int)gem.InstDamageMult;
        int critMult = (int)gem.InstCritMult;

        if (damageMult > 0) sb.AppendLine($"Bonus Damage: +{damageMult}");
        if (critMult > 0) sb.AppendLine($"Bonus Crit: +{critMult}");

        return sb.ToString().TrimEnd();
    }

    #endregion
}