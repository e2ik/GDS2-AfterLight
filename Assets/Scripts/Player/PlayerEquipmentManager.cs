using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEquipmentManager : MonoBehaviour
{
    [System.Serializable]
    public struct GearSlotDebugView
    {
        public EGearSlot Slot;
        public GearInstance EquippedGear;
    }

    [Header("Equipped Items")]
    [SerializeField] private WeaponDefinition equippedWeapon;
    [SerializeField] private PrimaryGemBehaviourDefinition specialAttackDef;
    [SerializeField] private SecondaryGemInstance secondaryGem = new SecondaryGemInstance();

    [Header("Inspector Debug View (Read-Only)")]
    [SerializeField] private List<GearSlotDebugView> equippedGearDebug = new List<GearSlotDebugView>();

    private Dictionary<EGearSlot, GearInstance> equippedGear = new Dictionary<EGearSlot, GearInstance>();

    public WeaponDefinition EquippedWeapon => equippedWeapon;
    public PrimaryGemBehaviourDefinition SpecialAttackDef => specialAttackDef;
    public SecondaryGemInstance SecondaryGem => secondaryGem;
    public IReadOnlyDictionary<EGearSlot, GearInstance> EquippedGear => equippedGear;

    public event System.Action OnEquipmentChanged;

    private void Awake()
    {
        InitializeGearSlots();
    }

    private void InitializeGearSlots()
    {
        foreach (EGearSlot gearType in Enum.GetValues(typeof(EGearSlot)))
        {
            if (!equippedGear.ContainsKey(gearType))
            {
                equippedGear.Add(gearType, null);
            }
        }
        UpdateDebugView();
    }

    public bool IsGemEquipped(SecondaryGemInstance gem)
    {
        if (gem == null || secondaryGem == null) return false;

        if (secondaryGem == gem) return true;

        if (!string.IsNullOrEmpty(secondaryGem.InstanceGUID) && !string.IsNullOrEmpty(gem.InstanceGUID))
        {
            return secondaryGem.InstanceGUID == gem.InstanceGUID;
        }

        return false;
    }

    public bool IsGearEquipped(GearInstance gear)
    {
        if (gear == null || string.IsNullOrEmpty(gear.InstTemplateID)) return false;

        var def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
        if (def != null && equippedGear.TryGetValue(def.Slot, out GearInstance equippedItem))
        {
            if (equippedItem == null) return false;

            if (equippedItem == gear) return true;

            if (!string.IsNullOrEmpty(equippedItem.InstanceGUID) && !string.IsNullOrEmpty(gear.InstanceGUID))
            {
                return equippedItem.InstanceGUID == gear.InstanceGUID;
            }
        }

        return false;
    }

    public void EquipWeapon(WeaponDefinition newWeapon)
    {
        equippedWeapon = newWeapon;
        OnEquipmentChanged?.Invoke();
    }

    public void EquipSpecialAttack(PrimaryGemBehaviourDefinition newSpecial)
    {
        specialAttackDef = newSpecial;
        OnEquipmentChanged?.Invoke();
    }

    public void EquipSecondaryGem(SecondaryGemInstance newGem)
    {
        secondaryGem = newGem;
        OnEquipmentChanged?.Invoke();
    }

    public void ClearWeapon()
    {
        equippedWeapon = null;
        OnEquipmentChanged?.Invoke();
    }

    public void ClearSpecialAttack()
    {
        specialAttackDef = null;
        OnEquipmentChanged?.Invoke();
    }

    public void ClearSecondaryGem()
    {
        secondaryGem = new SecondaryGemInstance();
        OnEquipmentChanged?.Invoke();
    }

    public void EquipGear(EGearSlot gearType, GearInstance newGear)
    {
        equippedGear[gearType] = newGear;
        UpdateDebugView();
        OnEquipmentChanged?.Invoke();
        Debug.Log($"[EquipmentManager] Equipped {gearType}: {newGear?.InstTemplateID ?? "None"}");
    }

    public void ClearGear(EGearSlot gearType)
    {
        if (equippedGear.ContainsKey(gearType))
        {
            equippedGear[gearType] = null;
            UpdateDebugView();
            OnEquipmentChanged?.Invoke();
        }
    }

    public GearInstance GetEquippedGear(EGearSlot gearType)
    {
        return equippedGear.TryGetValue(gearType, out GearInstance gear) ? gear : null;
    }

    public void ClearAllGear()
    {
        foreach (EGearSlot gearType in Enum.GetValues(typeof(EGearSlot)))
        {
            equippedGear[gearType] = null;
        }
        UpdateDebugView();
        OnEquipmentChanged?.Invoke();
    }

    private void UpdateDebugView()
    {
        equippedGearDebug.Clear();
        foreach (var kvp in equippedGear)
        {
            equippedGearDebug.Add(new GearSlotDebugView
            {
                Slot = kvp.Key,
                EquippedGear = kvp.Value
            });
        }
    }

    public List<EquippedGearSaveData> GetEquippedGearSaveData()
    {
        List<EquippedGearSaveData> saveDataList = new List<EquippedGearSaveData>();

        foreach (var pair in equippedGear)
        {
            if (pair.Value != null)
            {
                saveDataList.Add(new EquippedGearSaveData
                {
                    slot = pair.Key,
                    gearData = pair.Value
                });
            }
        }

        return saveDataList;
    }

    public void LoadEquippedGearSaveData(List<EquippedGearSaveData> savedGear)
    {
        ClearAllGear();

        if (savedGear == null) return;

        foreach (EquippedGearSaveData entry in savedGear)
        {
            if (entry != null && entry.gearData != null)
            {
                if (string.IsNullOrEmpty(entry.gearData.InstanceGUID))
                {
                    entry.gearData.InstanceGUID = System.Guid.NewGuid().ToString();
                }

                EquipGear(entry.slot, entry.gearData);
            }
        }
        OnEquipmentChanged?.Invoke();
    }

    public AttackContext GetModifiedAttackContext()
    {
        AttackContext context = new AttackContext
        {
            BaseAttackDamage = equippedWeapon != null ? equippedWeapon.BaseWeaponDamage : 0f,
            BaseAttackCrit = equippedWeapon != null ? equippedWeapon.BaseWeaponCrit : 0f
        };

        if (secondaryGem != null && !string.IsNullOrEmpty(secondaryGem.InstTemplateID))
        {
            SecondaryGemBehaviourDefinition secondaryDef = GameDatabase.GetSecondaryTemplateFromID(secondaryGem.InstTemplateID);
            secondaryDef?.Modify(ref context, secondaryGem);
        }

        float gearBonusAttack = 0f;

        foreach (KeyValuePair<EGearSlot, GearInstance> slot in equippedGear)
        {
            GearInstance gear = slot.Value;
            if (gear != null)
            {
                gearBonusAttack += gear.InstBonusAttack;
            }
        }
        context.BaseAttackDamage += gearBonusAttack;

        return context;
    }
}