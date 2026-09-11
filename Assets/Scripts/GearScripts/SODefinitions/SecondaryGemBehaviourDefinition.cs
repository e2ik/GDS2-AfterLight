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

    [Header("Damage by Rarity")]
    public RarityRange DamageByRarity = new RarityRange
    {
        Common = new Vector2(1f, 3f),
        Rare = new Vector2(3f, 5f),
        Epic = new Vector2(5f, 10f),
        Legendary = new Vector2(15f, 20f)
    };

    [Header("Crit by Rarity (0-1)")]
    public RarityRange CritByRarity = new RarityRange
    {
        Common = new Vector2(0.03f, 0.05f),
        Rare = new Vector2(0.05f, 0.10f),
        Epic = new Vector2(0.10f, 0.15f),
        Legendary = new Vector2(0.15f, 0.20f)
    };

    public abstract void Modify(ref AttackContext context, SecondaryGemInstance instance);

    public virtual SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        Vector2 dmgRange = DamageByRarity.GetRange(rarity);
        Vector2 critRange = CritByRarity.GetRange(rarity);

        SecondaryGemInstance newInstance = new SecondaryGemInstance
        {
            InstRolledDamageValue = Mathf.RoundToInt(Random.Range(dmgRange.x, dmgRange.y)),
            InstRolledCritValue = Mathf.RoundToInt(Random.Range(critRange.x, critRange.y) * 100f),
            InstTemplateID = TemplateID,
            InstanceGUID = System.Guid.NewGuid().ToString(),
            Rarity = rarity
        };
        return newInstance;
    }
}