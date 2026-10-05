using UnityEngine;

[CreateAssetMenu(fileName = "GreenModifierGemTemplate", menuName = "SecondaryTemplates/GreenModifierGemTemplate")]
public class GreenModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    // Skill Charge on hit or parry Gem
    [Header("Energy Charge by Rarity (out of 100)")]
    [SerializeField] private RarityRange chargeAmountByRarity = new RarityRange
    {
        Common = new Vector2(5f, 8f),
        Rare = new Vector2(9f, 12f),
        Epic = new Vector2(13f, 16f),
        Legendary = new Vector2(17f, 20f)
    };

    protected override SecondaryGemInstance CreateInstance(RarityLineRoller roller)
    {
        SecondaryGemInstance instance = base.CreateInstance(roller);

        instance.InstRolledChargeAmount = roller.RollValue(chargeAmountByRarity) / 100f;

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