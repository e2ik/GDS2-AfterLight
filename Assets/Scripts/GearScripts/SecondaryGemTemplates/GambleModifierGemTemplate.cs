using UnityEngine;

[CreateAssetMenu(fileName = "GambleModifierGemTemplate", menuName = "SecondaryTemplates/GambleModifierGemTemplate")]
public class GambleModifierGemTemplate : SecondaryGemBehaviourDefinition
{

    [SerializeField] private RarityRange maxDamageMultByRarity = new RarityRange();
    
    public override PassiveType GetPassiveType(SecondaryGemInstance instance)
    {
        return PassiveType.DamageMod;
    }

    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        float roll = Random.Range(0f,1f);
        float adjustedMult = instance.InstRolledMaxGambleMult * roll;
        context.BaseAttackDamage *= adjustedMult;
        Debug.Log($"Rolled {roll}. Adjusted Multiplier is {adjustedMult} Dealing {context.BaseAttackDamage}.");
    }
    public override SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        SecondaryGemInstance instance = base.CreateInstance(rarity);

        Vector2 range = maxDamageMultByRarity.GetRange(rarity);
        instance.InstRolledMaxGambleMult = Random.Range(range.x, range.y);

        return instance;
    }
}
