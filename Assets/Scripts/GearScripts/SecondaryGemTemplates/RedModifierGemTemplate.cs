using UnityEngine;

[CreateAssetMenu(fileName = "RedModifierGemTemplate", menuName = "SecondaryTemplates/RedModifierGemTemplate")]
public class RedModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    [SerializeField] private float dotTickInterval = 1f;
    [SerializeField] private float dotDuration = 3f;
    [SerializeField] private Vector2 dotPercentRange = new Vector2(5f, 15f);

    public override void Trigger(SecondaryGemInstance instance)
    {
        //trigger is for non-attack context passives (just parry atm)
    }

    public override SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        SecondaryGemInstance instance = base.CreateInstance(rarity);

        float multiplier = GetRarityMultiplier(rarity);
        instance.InstRolledDotPercent = Mathf.RoundToInt(Random.Range(dotPercentRange.x, dotPercentRange.y) * multiplier);

        return instance;
    }

    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        context.AppliesDot = true;
        context.DotDamagePercent = instance.InstRolledDotPercent / 100f;
        context.DotTickInterval = dotTickInterval;
        context.DotDuration = dotDuration;
    }
}