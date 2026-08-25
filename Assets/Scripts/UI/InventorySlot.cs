using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlot : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button actionButton;

    [Header("Equipped Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color equippedColor = Color.green;

    private SecondaryGemInstance currentSecondaryGem;
    private GearInstance currentGear;

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

    public void SetupSlot(SecondaryGemInstance gem)
    {
        currentSecondaryGem = gem;
        currentGear = null;

        var def = GameDatabase.GetSecondaryTemplateFromID(gem.InstTemplateID);
        if (def != null)
        {
            if (iconImage != null) iconImage.sprite = def.UISprite;
            if (nameText != null) nameText.text = def.UIName;
        }

        UpdateEquippedVisuals();
    }

    public void SetupSlot(GearInstance gear)
    {
        currentGear = gear;
        currentSecondaryGem = null;

        var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
        if (def != null)
        {
            if (iconImage != null) iconImage.sprite = def.UISprite;
            if (nameText != null) nameText.text = def.UIName;
        }

        UpdateEquippedVisuals();
    }

    private void OnSlotClicked()
    {
        Player player = FindFirstObjectByType<Player>();
        if (player == null || player.Equipment == null) return;

        PlayerEquipmentManager equipManager = player.Equipment;

        if (currentSecondaryGem != null)
        {
            if (equipManager.IsGemEquipped(currentSecondaryGem))
            {
                equipManager.ClearSecondaryGem();
                Debug.Log($"Unequipped Gem: {currentSecondaryGem.InstTemplateID}");
            }
            else
            {
                equipManager.EquipSecondaryGem(currentSecondaryGem);
                Debug.Log($"Equipped Gem: {currentSecondaryGem.InstTemplateID}");
            }
        }
        else if (currentGear != null)
        {
            var def = GameDatabase.GetGearTemplateFromID(currentGear.InstTemplateID);
            if (def != null)
            {
                if (equipManager.IsGearEquipped(currentGear))
                {
                    equipManager.ClearGear(def.Slot);
                    Debug.Log($"Unequipped Gear from slot: {def.Slot}");
                }
                else
                {
                    equipManager.EquipGear(def.Slot, currentGear);
                    Debug.Log($"Equipped Gear: {def.Slot}");
                }
            }
        }

        InventoryDisplay display = FindFirstObjectByType<InventoryDisplay>();
        if (display != null)
        {
            display.RefreshUI();
        }
    }

    private void UpdateEquippedVisuals()
    {
        if (actionButton == null || actionButton.image == null) return;

        Player player = FindFirstObjectByType<Player>();
        if (player == null || player.Equipment == null)
        {
            actionButton.image.color = normalColor;
            return;
        }

        bool isEquipped = false;

        if (currentSecondaryGem != null)
        {
            isEquipped = player.Equipment.IsGemEquipped(currentSecondaryGem);
        }
        else if (currentGear != null)
        {
            isEquipped = player.Equipment.IsGearEquipped(currentGear);
        }

        actionButton.image.color = isEquipped ? equippedColor : normalColor;
    }
}