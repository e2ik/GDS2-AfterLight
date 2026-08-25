using UnityEngine;

[CreateAssetMenu(fileName = "GreenModifierGemTemplate", menuName = "SecondaryTemplates/GreenModifierGemTemplate")]
public class GreenModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        Debug.Log($"Old Damage: {context.BaseAttackDamage}");
        context.BaseAttackDamage += instance.InstDamageMult;
        Debug.Log($"New Damage: {context.BaseAttackDamage}");
    }
}
