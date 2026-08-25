using UnityEngine;

public class PlayerEquipmentManager : MonoBehaviour
{
    [Header("Equipped Items")]
    [SerializeField] private WeaponDefinition equippedWeapon;
    [SerializeField] private PrimaryGemBehaviourDefinition specialAttackDef;
    [SerializeField] private SecondaryGemInstance secondaryGem = new SecondaryGemInstance();

    public WeaponDefinition EquippedWeapon => equippedWeapon;
    public PrimaryGemBehaviourDefinition SpecialAttackDef => specialAttackDef;
    public SecondaryGemInstance SecondaryGem => secondaryGem;

    public void EquipWeapon(WeaponDefinition newWeapon) => equippedWeapon = newWeapon;
    public void EquipSpecialAttack(PrimaryGemBehaviourDefinition newSpecial) => specialAttackDef = newSpecial;
    public void EquipSecondaryGem(SecondaryGemInstance newGem) => secondaryGem = newGem;

    public void ClearWeapon() => equippedWeapon = null;
    public void ClearSpecialAttack() => specialAttackDef = null;
    public void ClearSecondaryGem() => secondaryGem = new SecondaryGemInstance();

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

        return context;
    }
}