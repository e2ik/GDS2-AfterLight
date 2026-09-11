using UnityEngine;

[CreateAssetMenu(fileName = "BlueModifierGemTemplate", menuName = "SecondaryTemplates/BlueModifierGemTemplate")]
public class BlueModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    //reflect damage on parry
    [SerializeField] private Vector2 chargeAmountRange = new(0.01f, 0.1f);

    private RarityRange reflectAmountByRarity = new RarityRange
    {
        Common = new Vector2(0.01f, 0.25f),
        Rare = new Vector2(0.26f, 0.5f),
        Epic = new Vector2(0.51f, 0.75f),
        Legendary = new Vector2(0.76f, 1f)
    };
    
    public override SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        SecondaryGemInstance instance = base.CreateInstance(rarity);

        Vector2 range = reflectAmountByRarity.GetRange(rarity);
        instance.InstRolledChargeAmount = Random.Range(range.x, range.y);

        return instance;
    }
    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        context.BaseAttackDamage *= instance.InstRolledChargeAmount;
    }

    public override PassiveType Trigger(SecondaryGemInstance instance)
    {
        //trigger is for non-attack context passives (just parry atm)
        return PassiveType.Reflect;
    }
}
