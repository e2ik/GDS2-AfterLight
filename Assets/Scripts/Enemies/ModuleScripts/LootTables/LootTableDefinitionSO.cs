using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LootTableDefinitionSO", menuName = "Enemies/LootTable")]
public class LootTableDefinitionSO : ScriptableObject
{
    public enum ERepeatAvoidance { None, SameItem, SameType }

    [System.Serializable]
    public class LootEntry
    {
        public InventoryItemBase lootItem;
        public WorldItem worldItem;
        [Min(0f)] public float weight = 1f;
    }

    public List<LootEntry> possibleDrops = new List<LootEntry>();
    [Min(0f)] public float nothingWeight = 0f;

    [Header("Variety")]
    public ERepeatAvoidance avoidRepeat = ERepeatAvoidance.SameItem;
    public bool cycleThroughTable = true;

    [Header("Rarity Odds")]
    public bool overrideRarityOdds = false;
    public RarityWeights rarityOdds = new RarityWeights();

    [Header("Pop")]
    [SerializeField] private float popForce = 4f;
    [SerializeField] private float popSpreadX = 0.1f;

    private readonly List<LootEntry> candidates = new List<LootEntry>();
    private readonly List<LootEntry> filtered = new List<LootEntry>();

    public void SpawnInstance(Vector3 spawn)
    {
        LootEntry entry = PickEntry();
        if (entry == null) return;

        if (entry.lootItem == null || entry.worldItem == null)
        {
            Debug.LogWarning($"[LootTable] '{name}' picked an entry with no loot item or world item assigned.");
            return;
        }

        RarityWeights odds = overrideRarityOdds ? rarityOdds : null;

        Vector3 spawnPosition = spawn + new Vector3(0f, 0.5f, 0f);
        WorldItem droppedItem = Instantiate(entry.worldItem, spawnPosition, Quaternion.identity);
        droppedItem.Initialize(entry.lootItem, null, odds);

        Vector2 popDirection = new Vector2(Random.Range(-popSpreadX, popSpreadX), 1f).normalized;
        droppedItem.PopOut(popDirection, popForce);
    }

    public LootEntry PickEntry()
    {
        candidates.Clear();
        foreach (LootEntry entry in possibleDrops)
        {
            if (entry != null && entry.lootItem != null && entry.weight > 0f) candidates.Add(entry);
        }

        if (candidates.Count == 0) return null;

        List<LootEntry> pool = candidates;

        if (cycleThroughTable)
        {
            filtered.Clear();
            foreach (LootEntry entry in candidates)
            {
                if (!LootDropHistory.WasDroppedThisCycle(this, entry.lootItem)) filtered.Add(entry);
            }

            if (filtered.Count == 0)
            {
                LootDropHistory.ResetCycle(this);
            }
            else
            {
                pool = new List<LootEntry>(filtered);
            }
        }

        if (avoidRepeat != ERepeatAvoidance.None)
        {
            filtered.Clear();
            foreach (LootEntry entry in pool)
            {
                if (!LootDropHistory.IsRepeat(entry.lootItem, avoidRepeat)) filtered.Add(entry);
            }

            if (filtered.Count > 0) pool = new List<LootEntry>(filtered);
        }

        LootEntry picked = WeightedPick(pool);
        if (picked != null) LootDropHistory.Record(picked.lootItem, this);
        return picked;
    }

    private LootEntry WeightedPick(List<LootEntry> pool)
    {
        float total = nothingWeight;
        foreach (LootEntry entry in pool) total += entry.weight;

        if (total <= 0f) return null;

        float pick = Random.value * total;

        foreach (LootEntry entry in pool)
        {
            pick -= entry.weight;
            if (pick < 0f) return entry;
        }

        return null;
    }
}