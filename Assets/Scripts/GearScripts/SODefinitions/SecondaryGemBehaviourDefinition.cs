using UnityEditor;
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
    //These should be replaced with ranges for the rarity to roll on.
    public int rolledValue;

    public string TemplateID; 

    public abstract void Modify(ref AttackContext context, SecondaryGemInstance instance);

    public SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        int rolledDamage = GetRolledValue(rarity);
        int rolledCrit = GetRolledValue(rarity); // or a separate roll table if crit should scale differently

        SecondaryGemInstance newInstance = new SecondaryGemInstance
        {
            InstRolledDamageValue = rolledDamage,
            InstRolledCritValue = rolledCrit,
            InstTemplateID = TemplateID,
            InstanceGUID = System.Guid.NewGuid().ToString()
        };
        return newInstance;
    }

    private int GetRolledValue(ERarity rarity)
    {
        switch (rarity)
        {
            case ERarity.Common:
                return Random.Range(2,maxCommonValue);
            case ERarity.Rare:
                return Random.Range(maxCommonValue + 1, maxEpicValue);
            case ERarity.Epic:
                return Random.Range(maxRareValue + 1, maxEpicValue);
            case ERarity.Legendary:
                return Random.Range(maxEpicValue + 1, maxLegendaryValue);
            default:
                return 1;
        }
    }
}