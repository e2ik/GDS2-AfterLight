using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LootTableDefinitionSO", menuName = "Enemies/LootTable")]
public class LootTableDefinitionSO : ScriptableObject
{
    [System.Serializable]
    public class LootEntry
    {
        public InventoryItemBase lootItem;
        public WorldItem worldItem;
        [Min(0f)] public float weight = 1f;
    }

    public List<LootEntry> possibleDrops = new List<LootEntry>();
    [Min(0f)] public float nothingWeight = 0f;

    [Header("Rarity Odds")]
    public bool overrideRarityOdds = false;
    public RarityWeights rarityOdds = new RarityWeights();

    [Header("Pop")]
    [SerializeField] private float popForce = 4f;
    [SerializeField] private float popSpreadX = 0.1f;

    public void SpawnInstance(Vector3 spawn)
    {
        LootEntry entry = PickEntry();
        if (entry == null) return;

        if (entry.lootItem == null || entry.worldItem == null)
        {
            Debug.LogWarning($"[LootTable] '{name}' picked an entry with no loot item or world item assigned.");
            return;
        }

        ERarity? rarity = RarityWeights.UsesRarity(entry.lootItem) ? RarityWeights.Roll(overrideRarityOdds, rarityOdds) : (ERarity?)null;

        Vector3 spawnPosition = spawn + new Vector3(0f, 0.5f, 0f);
        WorldItem droppedItem = Instantiate(entry.worldItem, spawnPosition, Quaternion.identity);
        droppedItem.Initialize(entry.lootItem, rarity);

        Vector2 popDirection = new Vector2(Random.Range(-popSpreadX, popSpreadX), 1f).normalized;
        droppedItem.PopOut(popDirection, popForce);
    }

    private LootEntry PickEntry()
    {
        float total = nothingWeight;
        foreach (LootEntry entry in possibleDrops)
        {
            if (entry != null) total += entry.weight;
        }

        if (total <= 0f) return null;

        float pick = Random.value * total;

        foreach (LootEntry entry in possibleDrops)
        {
            if (entry == null || entry.weight <= 0f) continue;

            pick -= entry.weight;
            if (pick < 0f) return entry;
        }

        return null;
    }
}