using UnityEngine;

[CreateAssetMenu(fileName = "RedModifierGemTemplate", menuName = "SecondaryTemplates/RedModifierGemTemplate")]
public class RedModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        Debug.Log($"Old Damage: {context.BaseAttackDamage}");
        context.BaseAttackDamage *= instance.InstDamageMult;
        Debug.Log($"New Damage: {context.BaseAttackDamage}");
    }
}
