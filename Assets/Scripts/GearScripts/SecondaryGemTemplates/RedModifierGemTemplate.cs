using UnityEngine;

[CreateAssetMenu(fileName = "RedModifierGemTemplate", menuName = "SecondaryTemplates/RedModifierGemTemplate")]
public class RedModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    [SerializeField] private float dotTickInterval = 1f;
    [SerializeField] private float dotDuration = 3f;

    [Header("Dot Percent by Rarity")]
    [SerializeField]
    private RarityRange dotPercentByRarity = new RarityRange
    {
        Common = new Vector2(3f, 5f),
        Rare = new Vector2(5f, 10f),
        Epic = new Vector2(10f, 15f),
        Legendary = new Vector2(15f, 20f)
    };

    public override PassiveType Trigger(SecondaryGemInstance instance)
    {
        //trigger is for non-attack context passives (just parry atm)
        return PassiveType.DoT;
    }

    public override SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        SecondaryGemInstance instance = base.CreateInstance(rarity);

        Vector2 dotRange = dotPercentByRarity.GetRange(rarity);
        instance.InstRolledDotPercent = Mathf.RoundToInt(Random.Range(dotRange.x, dotRange.y));

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