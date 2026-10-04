using UnityEngine;

[CreateAssetMenu(fileName = "BlueModifierGemTemplate", menuName = "SecondaryTemplates/BlueModifierGemTemplate")]
public class BlueModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    //reflect damage on parry
    [Header("Reflect Percent by Rarity (%)")]
    [SerializeField] private RarityRange reflectAmountByRarity = new RarityRange
    {
        Common = new Vector2(1f, 10f),
        Rare = new Vector2(11f, 20f),
        Epic = new Vector2(21f, 30f),
        Legendary = new Vector2(31f, 40f)
    };

    protected override SecondaryGemInstance CreateInstance(RarityLineRoller roller)
    {
        SecondaryGemInstance instance = base.CreateInstance(roller);

        instance.InstRolledReflectPercent = roller.RollValue(reflectAmountByRarity) / 100f;

        return instance;
    }
    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        context.BaseAttackDamage *= instance.InstRolledReflectPercent;
    }

    public override PassiveType GetPassiveType(SecondaryGemInstance instance)
    {
        return PassiveType.Reflect;
    }
}