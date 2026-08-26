using UnityEngine;

public class EquipmentDisplay : MonoBehaviour
{
    [Header("Gear Slots")]
    [SerializeField] private EquipmentSlot bodySlot;
    [SerializeField] private EquipmentSlot bootsSlot;
    [SerializeField] private EquipmentSlot weaponSlot;
    [SerializeField] private EquipmentSlot primarySlot;
    [SerializeField] private EquipmentSlot secondarySlot;

    private PlayerEquipmentManager equipmentManager;

    private void OnDestroy()
    {
        UnbindManager();
    }

    public void RegisterEquipmentManager(PlayerEquipmentManager manager)
    {
        UnbindManager();

        equipmentManager = manager;

        if (equipmentManager != null)
        {
            equipmentManager.OnEquipmentChanged += RefreshEquipmentUI;
            RefreshEquipmentUI();
        }
    }

    private void UnbindManager()
    {
        if (equipmentManager != null)
        {
            equipmentManager.OnEquipmentChanged -= RefreshEquipmentUI;
        }
    }

    public void RefreshEquipmentUI()
    {
        if (equipmentManager == null)
        {
            equipmentManager = Object.FindFirstObjectByType<PlayerEquipmentManager>();
            if (equipmentManager == null) return;
        }

        GearInstance bodyGear = equipmentManager.GetEquippedGear(EGearSlot.Armor);
        UpdateGearSlotUI(bodySlot, bodyGear);

        GearInstance bootsGear = equipmentManager.GetEquippedGear(EGearSlot.Boots);
        UpdateGearSlotUI(bootsSlot, bootsGear);

        if (weaponSlot != null)
        {
            WeaponDefinition weapon = equipmentManager.EquippedWeapon;
            if (weapon != null)
            {
                weaponSlot.DisplayItem(weapon.UISprite, weapon.UIName);
            }
            else
            {
                weaponSlot.ClearSlot();
            }
        }

        if (primarySlot != null)
        {
            PrimaryGemBehaviourDefinition primary = equipmentManager.SpecialAttackDef;
            if (primary != null)
            {
                primarySlot.DisplayItem(primary.UISprite, primary.UIName);
            }
            else
            {
                primarySlot.ClearSlot();
            }
        }

        if (secondarySlot != null)
        {
            SecondaryGemInstance gem = equipmentManager.SecondaryGem;
            if (gem != null && !string.IsNullOrEmpty(gem.InstTemplateID))
            {
                var def = GameDatabase.GetSecondaryTemplateFromID(gem.InstTemplateID);
                if (def != null)
                {
                    secondarySlot.DisplayItem(def.UISprite, def.UIName);
                }
                else
                {
                    secondarySlot.ClearSlot();
                }
            }
            else
            {
                secondarySlot.ClearSlot();
            }
        }
    }

    private void UpdateGearSlotUI(EquipmentSlot slotUI, GearInstance gear)
    {
        if (slotUI == null) return;

        if (gear != null && !string.IsNullOrEmpty(gear.InstTemplateID))
        {
            var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
            if (def != null)
            {
                slotUI.DisplayItem(def.UISprite, def.UIName);
                return;
            }
        }

        slotUI.ClearSlot();
    }
}