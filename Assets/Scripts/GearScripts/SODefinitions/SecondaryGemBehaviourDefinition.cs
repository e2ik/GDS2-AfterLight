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
    public float DamageMult;
    private float CritMult;
    private float SizeMult;

    public string TemplateID; 

    public abstract void Modify(ref AttackContext context, SecondaryGemInstance instance);

    public SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        Debug.Log($"Created Instance of {UIName}. Rarity: {rarity}");
        SecondaryGemInstance newInstance = new SecondaryGemInstance
        {
            InstDamageMult = Random.Range(1f,DamageMult),
            InstCritMult = CritMult,
            InstSizeMult = SizeMult,
            InstTemplateID = TemplateID,
            InstanceGUID = System.Guid.NewGuid().ToString()
        };
        return newInstance;
    }
}