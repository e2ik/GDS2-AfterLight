using FMODUnity;
using UnityEngine;

public enum ERarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

public enum SGemType
{
    Parry,
    Attack,
    Skill
}

public enum PassiveType
{
    Energy,
    Reflect,
    DoT,
    DamageMod
}

[CreateAssetMenu(fileName = "SecondaryGemBehaviourDefintion", menuName = "ScriptableObjects/SecondaryGemBehaviourDefinition")]
public abstract class SecondaryGemBehaviourDefinition : InventoryItemBase, ISecondaryGemBehaviour
{
    public string TemplateID;
    public SGemType GemType;

    public EventReference basicPickupSound;

    // [Header("Damage by Rarity")]
    // public RarityRange DamageByRarity = new RarityRange
    // {
    //     Common = new Vector2(1f, 3f),
    //     Rare = new Vector2(3f, 5f),
    //     Epic = new Vector2(5f, 10f),
    //     Legendary = new Vector2(15f, 20f)
    // };

    // [Header("Crit by Rarity (0-1)")]
    // public RarityRange CritByRarity = new RarityRange
    // {
    //     Common = new Vector2(0.03f, 0.05f),
    //     Rare = new Vector2(0.05f, 0.10f),
    //     Epic = new Vector2(0.10f, 0.15f),
    //     Legendary = new Vector2(0.15f, 0.20f)
    // };

    public abstract void Modify(ref AttackContext context, SecondaryGemInstance instance);
    public abstract PassiveType GetPassiveType(SecondaryGemInstance instance);

    public SecondaryGemInstance CreateInstance(ERarity rarity) => Roll(RarityLineRoller.Fixed(rarity));

    public SecondaryGemInstance Roll(RarityLineRoller roller)
    {
        SecondaryGemInstance instance = CreateInstance(roller);
        instance.Rarity = roller.Final;
        instance.LineRarities = new System.Collections.Generic.List<ERarity>(roller.Lines);
        return instance;
    }

    protected virtual SecondaryGemInstance CreateInstance(RarityLineRoller roller)
    {
        SecondaryGemInstance newInstance = new SecondaryGemInstance
        {
            InstRolledDamageValue = Mathf.RoundToInt(roller.RollValue(DamageByRarity)),
            InstRolledCritValue = Mathf.RoundToInt(roller.RollValue(CritByRarity) * 100f),
            InstTemplateID = TemplateID,
            InstanceGUID = System.Guid.NewGuid().ToString(),
            Type = GemType
        };
        return newInstance;
    }
}