using System;

public enum TooltipPanel
{
    None,
    Loot,
    Inventory
}

public class TooltipActions
{
    public TooltipPanel Panel;
    public Action Equip;
    public Func<bool> CanEquip;
    public Action Delete;
    public Func<bool> CanDelete;

    public bool EquipAvailable => Equip != null && (CanEquip == null || CanEquip());
    public bool EquipHintAvailable => CanEquip != null && CanEquip();
    public bool DeleteAvailable => Delete != null && (CanDelete == null || CanDelete());
}

public static class ItemActionFactory
{
    public static TooltipActions ForLoot(object item)
    {
        if (!TryGetManagers(item, out PlayerEquipmentManager equip, out PlayerInventoryManager inventory)) return null;

        return new TooltipActions
        {
            Panel = TooltipPanel.Loot,
            Equip = () => EquipItem(item, equip),
            CanEquip = () => !IsItemEquipped(item, equip)
        };
    }

    public static TooltipActions ForInventory(object item)
    {
        if (!TryGetManagers(item, out PlayerEquipmentManager equip, out PlayerInventoryManager inventory)) return null;

        return new TooltipActions
        {
            Panel = TooltipPanel.Inventory,
            CanEquip = () => !IsItemEquipped(item, equip),
            Delete = () => inventory.RemoveItem(item),
            CanDelete = () => !IsItemEquipped(item, equip) && IsItemDeletable(item)
        };
    }

    public static string BuildTooltipBody(object item)
    {
        if (item == null) return null;

        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        PlayerEquipmentManager equip = player != null ? player.Equipment : null;
        if (equip == null) return null;

        bool isEquipped = IsItemEquipped(item, equip);

        switch (item)
        {
            case SecondaryGemInstance gem:
            {
                SecondaryGemInstance compare = (!isEquipped && !equip.IsSecondaryGemSlotEmpty()) ? equip.SecondaryGem : null;
                return ItemTooltipTextBuilder.BuildSecondaryGemTooltip(gem, compare);
            }

            case GearInstance gear:
            {
                var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
                if (def == null) return null;

                GearInstance compare = !isEquipped ? equip.GetEquippedGear(def.Slot) : null;
                return ItemTooltipTextBuilder.BuildGearTooltip(gear, def.Slot.ToString(), compare);
            }

            case PrimaryGemInstance primary:
            {
                var def = GameDatabase.GetPrimaryTemplateFromID(primary.InstTemplateID);
                return def != null ? ItemTooltipTextBuilder.BuildPrimaryGemTooltip(def) : null;
            }

            case WeaponInstance weapon:
            {
                WeaponInstance compare = !isEquipped ? equip.EquippedWeapon : null;
                return ItemTooltipTextBuilder.BuildWeaponTooltip(weapon, compare);
            }

            default:
                return null;
        }
    }

    private static bool TryGetManagers(object item, out PlayerEquipmentManager equip, out PlayerInventoryManager inventory)
    {
        equip = null;
        inventory = null;

        if (item == null) return false;

        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null) return false;

        equip = player.Equipment;
        inventory = player.Inventory;
        return equip != null && inventory != null;
    }

    private static InventoryItemBase GetDefinition(object item)
    {
        switch (item)
        {
            case SecondaryGemInstance gem:
                return GameDatabase.GetSecondaryTemplateFromID(gem.InstTemplateID);

            case GearInstance gear:
                return GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);

            case PrimaryGemInstance primary:
                return GameDatabase.GetPrimaryTemplateFromID(primary.InstTemplateID);

            case WeaponInstance weapon:
                return GameDatabase.GetWeaponTemplateFromID(weapon.InstTemplateID);

            default:
                return null;
        }
    }

    private static bool IsItemDeletable(object item)
    {
        InventoryItemBase definition = GetDefinition(item);
        return definition == null || definition.Deletable;
    }

    private static bool IsItemEquipped(object item, PlayerEquipmentManager equip)
    {
        switch (item)
        {
            case SecondaryGemInstance gem:
                return equip.IsGemEquipped(gem);

            case GearInstance gear:
                return equip.IsGearEquipped(gear);

            case WeaponInstance weapon:
                return equip.IsWeaponEquipped(weapon);

            case PrimaryGemInstance primary:
            {
                var def = GameDatabase.GetPrimaryTemplateFromID(primary.InstTemplateID);
                return def != null && equip.SpecialAttackDef == def;
            }

            default:
                return false;
        }
    }

    private static void EquipItem(object item, PlayerEquipmentManager equip)
    {
        switch (item)
        {
            case SecondaryGemInstance gem:
                equip.EquipSecondaryGem(gem);
                break;

            case GearInstance gear:
            {
                var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
                if (def != null) equip.EquipGear(def.Slot, gear);
                break;
            }

            case PrimaryGemInstance primary:
            {
                var def = GameDatabase.GetPrimaryTemplateFromID(primary.InstTemplateID);
                if (def != null) equip.EquipSpecialAttack(def);
                break;
            }

            case WeaponInstance weapon:
                equip.EquipWeapon(weapon);
                break;
        }
    }
}