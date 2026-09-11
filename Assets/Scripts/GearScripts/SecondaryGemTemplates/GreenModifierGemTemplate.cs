using UnityEngine;

[CreateAssetMenu(fileName = "GreenModifierGemTemplate", menuName = "SecondaryTemplates/GreenModifierGemTemplate")]
public class GreenModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    // Skill Charge on hit or parry Gem
    [SerializeField] private Vector2 chargeAmountRange = new(0.01f, 0.1f);
    public override SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        SecondaryGemInstance instance = base.CreateInstance(rarity);

        float multiplier = GetRarityMultiplier(rarity);
        instance.InstRolledChargeAmount = Random.Range(chargeAmountRange.x, chargeAmountRange.y) * multiplier;

        return instance;
    }
    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        // no longer required we implement the effect here
        context.ChargesSkillMeter = true;
        context.ChargeAmount = instance.InstRolledChargeAmount;
    }

    public override void Trigger(SecondaryGemInstance instance)
    {
        //trigger is for non-attack context passives (just parry atm)
        
    }
}
