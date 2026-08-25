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

    public void EquipWeapon(WeaponDefinition newWeapon) => equippedWeapon = newWeapon;
    public void EquipSpecialAttack(PrimaryGemBehaviourDefinition newSpecial) => specialAttackDef = newSpecial;
    public void EquipSecondaryGem(SecondaryGemInstance newGem) => secondaryGem = newGem;

    public void ClearWeapon() => equippedWeapon = null;
    public void ClearSpecialAttack() => specialAttackDef = null;
    public void ClearSecondaryGem() => secondaryGem = new SecondaryGemInstance();

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
                EquipGear(entry.slot, entry.gearData);
            }
        }
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