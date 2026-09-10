using UnityEngine;

[CreateAssetMenu(fileName = "GreenModifierGemTemplate", menuName = "SecondaryTemplates/GreenModifierGemTemplate")]
public class GreenModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        // no longer required we implement the effect here
    }
}
