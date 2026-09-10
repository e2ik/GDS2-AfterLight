using UnityEngine;

public enum ERarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

[CreateAssetMenu(fileName = "SecondaryGemBehaviourDefintion", menuName = "ScriptableObjects/SecondaryGemBehaviourDefinition")]

public abstract class SecondaryGemBehaviourDefinition : InventoryItemBase, ISecondaryGemBehaviour
{
    public string TemplateID;

    public abstract void Modify(ref AttackContext context, SecondaryGemInstance instance);

    public virtual SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        int rolledDamage = GetRolledValue(rarity);
        int rolledCrit = GetRolledValue(rarity);

        SecondaryGemInstance newInstance = new SecondaryGemInstance
        {
            InstRolledDamageValue = rolledDamage,
            InstRolledCritValue = rolledCrit,
            InstTemplateID = TemplateID,
            InstanceGUID = System.Guid.NewGuid().ToString()
        };
        return newInstance;
    }

    protected int GetRolledValue(ERarity rarity)
    {
        switch (rarity)
        {
            case ERarity.Common:
                return Random.Range(2, maxCommonValue);
            case ERarity.Rare:
                return Random.Range(maxCommonValue + 1, maxRareValue);
            case ERarity.Epic:
                return Random.Range(maxRareValue + 1, maxEpicValue);
            case ERarity.Legendary:
                return Random.Range(maxEpicValue + 1, maxLegendaryValue);
            default:
                return 1;
        }
    }

    protected float GetRarityMultiplier(ERarity rarity)
    {
        switch (rarity)
        {
            case ERarity.Common: return 1.0f;
            case ERarity.Rare: return 1.3f;
            case ERarity.Epic: return 1.6f;
            case ERarity.Legendary: return 2.0f;
            default: return 1.0f;
        }
    }
}