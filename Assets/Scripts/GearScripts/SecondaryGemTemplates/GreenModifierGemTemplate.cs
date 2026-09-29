using UnityEngine;

[CreateAssetMenu(fileName = "GreenModifierGemTemplate", menuName = "SecondaryTemplates/GreenModifierGemTemplate")]
public class GreenModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    // Skill Charge on hit or parry Gem
    [SerializeField] private Vector2 chargeAmountRange = new(0.01f, 0.1f);

    private RarityRange chargeAmountByRarity = new RarityRange
    {
        Common = new Vector2(0.05f, 0.08f),
        Rare = new Vector2(0.09f, 0.12f),
        Epic = new Vector2(0.13f, 0.16f),
        Legendary = new Vector2(0.17f, 0.2f)
    };
    
    public override SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        SecondaryGemInstance instance = base.CreateInstance(rarity);

        Vector2 range = chargeAmountByRarity.GetRange(rarity);
        instance.InstRolledChargeAmount = Random.Range(range.x, range.y);

        return instance;
    }
    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        context.ChargesSkillMeter = true;
        context.ChargeAmount = instance.InstRolledChargeAmount;
    }

    public override PassiveType GetPassiveType(SecondaryGemInstance instance)
    {
        return PassiveType.Energy;
    }
}
