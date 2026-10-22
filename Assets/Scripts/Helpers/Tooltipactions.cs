using System;
using System.Collections.Generic;
using UnityEngine;

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

    public static TooltipActions ForEquipped(object item)
    {
        if (!TryGetManagers(item, out PlayerEquipmentManager equip, out PlayerInventoryManager inventory)) return null;

        return new TooltipActions
        {
            Panel = TooltipPanel.Inventory,
            CanEquip = () => CanUnequip(item, equip),
            EquipLabel = "Unequip"
        };
    }

    public static bool CanUnequip(object item, PlayerEquipmentManager equip)
    {
        return item != null && !(item is WeaponInstance) && IsEquippableType(item) && IsItemEquipped(item, equip);
    }

    private class WorseCandidate
    {
        public object Item;
        public int[] Stats;
        public bool Equipped;
        public bool InInventory;
        public bool IsNew;
        public ERarity Rarity;
        public int PickupOrder;
        public string Guid;
    }

    public static bool IsWorseThanOwned(object item, PlayerInventoryManager inventory, PlayerEquipmentManager equip)
    {
        if (item == null || inventory == null || equip == null || inventory.currentInventory == null) return false;

        Dictionary<string, List<WorseCandidate>> incoming = new Dictionary<string, List<WorseCandidate>>();
        switch (item)
        {
            case GearInstance gear: AddGear(incoming, gear, equip, false); break;
            case WeaponInstance weapon: AddWeapon(incoming, weapon, equip, false); break;
            case SecondaryGemInstance gem: AddGem(incoming, gem, equip, false); break;
            default: return false;
        }

        Dictionary<string, List<WorseCandidate>> owned = BuildOwnedGroups(inventory, equip);

        foreach (KeyValuePair<string, List<WorseCandidate>> pair in incoming)
        {
            if (!owned.TryGetValue(pair.Key, out List<WorseCandidate> others)) continue;

            foreach (WorseCandidate candidate in pair.Value)
            {
                foreach (WorseCandidate other in others)
                {
                    if (other.Rarity >= candidate.Rarity && IsBetterOrEqual(other.Stats, candidate.Stats)) return true;
                }
            }
        }

        return false;
    }

    private static Dictionary<string, List<WorseCandidate>> BuildOwnedGroups(PlayerInventoryManager inventory, PlayerEquipmentManager equip)
    {
        Dictionary<string, List<WorseCandidate>> groups = new Dictionary<string, List<WorseCandidate>>();

        if (inventory.currentInventory.GearInstances != null)
        {
            foreach (GearInstance gear in inventory.currentInventory.GearInstances)
                AddGear(groups, gear, equip, true);
        }

        foreach (KeyValuePair<EGearSlot, GearInstance> pair in equip.EquippedGear)
            if (pair.Value != null && !ContainsCandidate(groups, pair.Value, pair.Value.InstanceGUID)) AddGear(groups, pair.Value, equip, false);

        if (inventory.currentInventory.SecondaryGems != null)
        {
            foreach (SecondaryGemInstance gem in inventory.currentInventory.SecondaryGems)
                AddGem(groups, gem, equip, true);
        }

        SecondaryGemInstance equippedGem = equip.SecondaryGem;
        if (equippedGem != null && !ContainsCandidate(groups, equippedGem, equippedGem.InstanceGUID)) AddGem(groups, equippedGem, equip, false);

        if (inventory.currentInventory.Weapons != null)
        {
            foreach (WeaponInstance weapon in inventory.currentInventory.Weapons)
                AddWeapon(groups, weapon, equip, true);
        }

        WeaponInstance equippedWeapon = equip.EquippedWeapon;
        if (equippedWeapon != null && !ContainsCandidate(groups, equippedWeapon, equippedWeapon.InstanceGUID)) AddWeapon(groups, equippedWeapon, equip, false);

        return groups;
    }

    public static List<object> GetWorseItems(PlayerInventoryManager inventory, PlayerEquipmentManager equip, bool keepNewItems)
    {
        List<object> result = new List<object>();
        if (inventory == null || equip == null || inventory.currentInventory == null) return result;

        Dictionary<string, List<WorseCandidate>> groups = BuildOwnedGroups(inventory, equip);

        foreach (List<WorseCandidate> candidates in groups.Values)
        {
            foreach (WorseCandidate candidate in candidates)
            {
                if (candidate.Equipped || !candidate.InInventory) continue;
                if (keepNewItems && candidate.IsNew) continue;
                if (!IsItemDeletable(candidate.Item)) continue;

                foreach (WorseCandidate other in candidates)
                {
                    if (other == candidate || other.Rarity < candidate.Rarity) continue;
                    if (!IsBetterOrEqual(other.Stats, candidate.Stats)) continue;

                    if (IsStrictlyBetter(other.Stats, candidate.Stats) || RanksAbove(other, candidate))
                    {
                        result.Add(candidate.Item);
                        break;
                    }
                }
            }
        }

        return result;
    }

    private static void AddGear(Dictionary<string, List<WorseCandidate>> groups, GearInstance gear, PlayerEquipmentManager equip, bool inInventory)
    {
        if (gear == null) return;

        GearDefinition def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
        if (def == null) return;

        AddCandidate(groups, "gear:" + def.Slot, new WorseCandidate
        {
            Item = gear,
            Stats = new[]
            {
                (int)gear.InstBonusAttack,
                (int)gear.InstBonusDefense,
                (int)gear.InstBonusHumanity,
                Mathf.RoundToInt(gear.InstBonusCrit * 1000f)
            },
            Equipped = equip.IsGearEquipped(gear),
            InInventory = inInventory,
            IsNew = gear.IsNew,
            Rarity = gear.Rarity,
            PickupOrder = gear.PickupOrder,
            Guid = gear.InstanceGUID
        });
    }

    private static void AddGem(Dictionary<string, List<WorseCandidate>> groups, SecondaryGemInstance gem, PlayerEquipmentManager equip, bool inInventory)
    {
        if (gem == null || string.IsNullOrEmpty(gem.InstTemplateID)) return;

        AddCandidate(groups, "gem:" + gem.InstTemplateID, new WorseCandidate
        {
            Item = gem,
            Stats = new[]
            {
                gem.InstRolledDamageValue,
                gem.InstRolledCritValue,
                gem.InstRolledDotPercent,
                Mathf.RoundToInt(gem.InstRolledChargeAmount * 100f),
                Mathf.RoundToInt(gem.InstRolledReflectPercent * 1000f),
                Mathf.RoundToInt(gem.InstRolledMaxGambleMult * 10f)
            },
            Equipped = equip.IsGemEquipped(gem),
            InInventory = inInventory,
            IsNew = gem.IsNew,
            Rarity = gem.Rarity,
            PickupOrder = gem.PickupOrder,
            Guid = gem.InstanceGUID
        });
    }

    private static void AddWeapon(Dictionary<string, List<WorseCandidate>> groups, WeaponInstance weapon, PlayerEquipmentManager equip, bool inInventory)
    {
        if (weapon == null) return;

        AddCandidate(groups, "weapon", new WorseCandidate
        {
            Item = weapon,
            Stats = new[]
            {
                Mathf.RoundToInt(weapon.InstRolledDamage * 100f),
                Mathf.RoundToInt(weapon.InstRolledRange * 100f),
                Mathf.RoundToInt(weapon.InstRolledCrit * 1000f),
                Mathf.RoundToInt(weapon.InstRolledAttack * 100f),
                Mathf.RoundToInt(weapon.InstRolledDefense * 100f)
            },
            Equipped = equip.IsWeaponEquipped(weapon),
            InInventory = inInventory,
            IsNew = weapon.IsNew,
            Rarity = weapon.Rarity,
            PickupOrder = weapon.PickupOrder,
            Guid = weapon.InstanceGUID
        });
    }

    private static void AddCandidate(Dictionary<string, List<WorseCandidate>> groups, string key, WorseCandidate candidate)
    {
        if (!groups.TryGetValue(key, out List<WorseCandidate> list))
        {
            list = new List<WorseCandidate>();
            groups.Add(key, list);
        }

        list.Add(candidate);
    }

    private static bool ContainsCandidate(Dictionary<string, List<WorseCandidate>> groups, object item, string guid)
    {
        foreach (List<WorseCandidate> list in groups.Values)
        {
            foreach (WorseCandidate entry in list)
            {
                if (entry.Item == item) return true;
                if (!string.IsNullOrEmpty(guid) && entry.Guid == guid) return true;
            }
        }

        return false;
    }

    private static bool IsBetterOrEqual(int[] a, int[] b)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i] < b[i]) return false;

        return true;
    }

    private static bool IsStrictlyBetter(int[] a, int[] b)
    {
        if (!IsBetterOrEqual(a, b)) return false;

        for (int i = 0; i < a.Length; i++)
            if (a[i] > b[i]) return true;

        return false;
    }

    private static bool RanksAbove(WorseCandidate a, WorseCandidate b)
    {
        if (a.Equipped != b.Equipped) return a.Equipped;

        if (a.Rarity != b.Rarity) return a.Rarity > b.Rarity;

        if (a.PickupOrder != b.PickupOrder) return a.PickupOrder < b.PickupOrder;

        return string.CompareOrdinal(a.Guid, b.Guid) < 0;
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