using UnityEngine;

[CreateAssetMenu(fileName = "RedModifierGemTemplate", menuName = "SecondaryTemplates/RedModifierGemTemplate")]
public class RedModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    [SerializeField] private float dotTickInterval = 1f;
    [SerializeField] private float dotDuration = 3f;
    [SerializeField, Min(1)] private int attackDotMaxStacks = 3;

    [Header("Dot Percent by Rarity")]
    [SerializeField]
    private RarityRange dotPercentByRarity = new RarityRange
    {
        Common = new Vector2(3f, 5f),
        Rare = new Vector2(5f, 10f),
        Epic = new Vector2(10f, 15f),
        Legendary = new Vector2(15f, 20f)
    };

    protected override SecondaryGemInstance CreateInstance(RarityLineRoller roller)
    {
        SecondaryGemInstance instance = base.CreateInstance(roller);

        instance.InstRolledDotPercent = Mathf.RoundToInt(roller.RollValue(dotPercentByRarity));

        return instance;
    }

    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        context.AppliesDot = true;
        context.DotDamagePercent = instance.InstRolledDotPercent / 100f;
        context.DotTickInterval = dotTickInterval;
        context.DotDuration = dotDuration;
        context.DotMaxStacks = instance.Type == SGemType.Attack ? attackDotMaxStacks : 1;
    }

    public override PassiveType GetPassiveType(SecondaryGemInstance instance)
    {
        return PassiveType.DoT;
    }
}