using UnityEngine;

public static class ItemGenerator
{
    public static ItemInstance Generate(ItemSO baseItem)
    {
        var instance = new ItemInstance
        {
            instanceId = System.Guid.NewGuid().ToString(),
            itemId = baseItem.itemId,
            rolledDamage = Random.Range(baseItem.minDamage, baseItem.maxDamage + 1)
        };

        instance.rolledStats.Add(new StatRoll
        {
            statName = "attack",
            value = Random.Range(baseItem.minAttack, baseItem.maxAttack + 1)
        });
        instance.rolledStats.Add(new StatRoll
        {
            statName = "defense",
            value = Random.Range(baseItem.minDefense, baseItem.maxDefense + 1)
        });
        instance.rolledStats.Add(new StatRoll
        {
            statName = "humanity",
            value = Random.Range(baseItem.minHumanity, baseItem.maxHumanity + 1)
        });

        return instance;
    }
}