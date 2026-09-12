using UnityEngine;

[CreateAssetMenu(fileName = "BlueModifierGemTemplate", menuName = "SecondaryTemplates/BlueModifierGemTemplate")]
public class BlueModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    //reflect damage on parry
    [SerializeField] private Vector2 chargeAmountRange = new(0.01f, 0.1f);

    private RarityRange reflectAmountByRarity = new RarityRange
    {
        Common = new Vector2(0.01f, 0.1f),
        Rare = new Vector2(0.11f, 0.2f),
        Epic = new Vector2(0.21f, 0.3f),
        Legendary = new Vector2(0.31f, 4f)
    };
    
    public override SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        SecondaryGemInstance instance = base.CreateInstance(rarity);

        Vector2 range = reflectAmountByRarity.GetRange(rarity);
        instance.InstRolledReflectPercent = Random.Range(range.x, range.y);

        return instance;
    }
    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        context.BaseAttackDamage *= instance.InstRolledReflectPercent;
    }

    public override PassiveType GetPassiveType(SecondaryGemInstance instance)
    {
        return PassiveType.Reflect;
    }
}
