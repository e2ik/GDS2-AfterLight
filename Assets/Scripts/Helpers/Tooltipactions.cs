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
    public string EquipLabel = "Equip";

    public bool EquipAvailable => Equip != null && (CanEquip == null || CanEquip());
    public bool EquipHintAvailable => CanEquip != null && CanEquip();
    public bool DeleteAvailable => Delete != null && (CanDelete == null || CanDelete());
}

public static class ItemActionFactory
{
    public static TooltipActions ForLoot(object item)
    {
        if (!TryGetManagers(item, out PlayerEquipmentManager equip, out PlayerInventoryManager inventory)) return null;

        bool equippable = IsEquippableType(item);

        return new TooltipActions
        {
            Panel = TooltipPanel.Loot,
            Equip = equippable ? () => EquipItem(item, equip) : null,
            CanEquip = equippable ? () => !IsItemEquipped(item, equip) : null
        };
    }

    public static TooltipActions ForInventory(object item)
    {
        if (!TryGetManagers(item, out PlayerEquipmentManager equip, out PlayerInventoryManager inventory)) return null;

        bool equippable = IsEquippableType(item);
        bool readable = item is LoreItemInstance;

        return new TooltipActions
        {
            Panel = TooltipPanel.Inventory,
            CanEquip = equippable ? () => !IsItemEquipped(item, equip) : (readable ? () => true : null),
            EquipLabel = readable ? "Read" : "Equip",
            Delete = () => inventory.RemoveItem(item),
            CanDelete = () => !IsItemEquipped(item, equip) && IsItemDeletable(item)
        };
    }

    private static bool IsEquippableType(object item)
    {
        return item is SecondaryGemInstance
            || item is GearInstance
            || item is WeaponInstance
            || item is PrimaryGemInstance;
    }

    public static PlayerEquipmentManager GetEquipment()
    {
        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        return player != null ? player.Equipment : null;
    }

    public static SecondaryGemInstance GetComparableGem(SecondaryGemInstance gem, PlayerEquipmentManager equip)
    {
        if (gem == null || equip == null || equip.IsSecondaryGemSlotEmpty()) return null;
        if (equip.IsGemEquipped(gem)) return null;

        return equip.SecondaryGem;
    }

    public static string BuildTooltipBody(object item)
    {
        if (item == null) return null;

        PlayerEquipmentManager equip = GetEquipment();
        bool isEquipped = equip != null && IsItemEquipped(item, equip);

        switch (item)
        {
            case SecondaryGemInstance gem:
                return ItemTooltipTextBuilder.BuildSecondaryGemTooltip(gem, GetComparableGem(gem, equip));

            case GearInstance gear:
            {
                var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
                if (def == null) return null;

                GearInstance compare = !isEquipped && equip != null ? equip.GetEquippedGear(def.Slot) : null;
                return ItemTooltipTextBuilder.BuildGearTooltip(gear, def.Slot.ToString(), compare);
            }

            case PrimaryGemInstance primary:
            {
                var def = GameDatabase.GetPrimaryTemplateFromID(primary.InstTemplateID);
                return def != null ? ItemTooltipTextBuilder.BuildPrimaryGemTooltip(def) : null;
            }

            case WeaponInstance weapon:
            {
                WeaponInstance compare = !isEquipped && equip != null ? equip.EquippedWeapon : null;
                return ItemTooltipTextBuilder.BuildWeaponTooltip(weapon, compare);
            }

            case KeyInstance key:
            {
                var def = GameDatabase.GetKeyTemplateFromID(key.InstItemID);
                return def != null ? def.Description : null;
            }

            case LoreItemInstance lore:
            {
                var def = GameDatabase.GetLoreItemTemplateFromID(lore.InstItemID);
                return def != null ? def.Description : null;
            }

            case LoreSetDisplayInfo loreSet:
            {
                if (loreSet.RepresentativeInstance == null) return null;

                var def = GameDatabase.GetLoreItemTemplateFromID(loreSet.RepresentativeInstance.InstItemID);
                if (def == null) return null;

                return loreSet.IsComplete
                    ? def.Description
                    : $"{def.Description}\n\n({loreSet.OwnedCount}/{loreSet.TotalPieces} pages found)";
            }

            default:
                return null;
        }
    }

    public static void ToggleEquip(object item, PlayerEquipmentManager equip)
    {
        if (item == null || equip == null || !IsEquippableType(item)) return;
        if (GetDefinition(item) == null) return;

        if (IsItemEquipped(item, equip)) UnequipItem(item, equip);
        else EquipItem(item, equip);
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

    public static InventoryItemBase GetDefinition(object item)
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

            case KeyInstance key:
                return GameDatabase.GetKeyTemplateFromID(key.InstItemID);

            case LoreItemInstance loreItem:
                return GameDatabase.GetLoreItemTemplateFromID(loreItem.InstItemID);

            case LoreSetDisplayInfo loreSet:
                return loreSet.RepresentativeInstance != null
                    ? GameDatabase.GetLoreItemTemplateFromID(loreSet.RepresentativeInstance.InstItemID)
                    : null;

            default:
                return null;
        }
    }

    private static bool IsItemDeletable(object item)
    {
        InventoryItemBase definition = GetDefinition(item);
        return definition == null || definition.Deletable;
    }

    public static bool IsItemEquipped(object item, PlayerEquipmentManager equip)
    {
        if (equip == null) return false;

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

            default:
                return;
        }

        equip.PlayEquipSound(item);
    }

    private static void UnequipItem(object item, PlayerEquipmentManager equip)
    {
        switch (item)
        {
            case SecondaryGemInstance:
                equip.ClearSecondaryGem();
                break;

            case GearInstance gear:
            {
                var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
                if (def != null) equip.ClearGear(def.Slot);
                break;
            }

            case PrimaryGemInstance:
                equip.ClearSpecialAttack();
                break;

            // Weapons are never unequipped by clicking them (same as before).
        }
    }
}