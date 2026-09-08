using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Enemies;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

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
    //List of Possible Drops
    public List<LootEntry> possibleDrops = new List<LootEntry>();
    //Function to spawn the drop
    public void SpawnInstance(Vector3 spawn)
    {
        LootEntry entry = GetItemToInstance();
        if (entry == null)
        {
            Debug.LogError($"Entry Not Assigned");
            return;
        }

        Vector3 spawnPosition = spawn + new Vector3(0f, 0.5f, 0f);

        WorldItem droppedItem = Instantiate(entry.worldItem, spawnPosition, Quaternion.identity);
        droppedItem.Initialize(entry.lootItem);

        float randomX = Random.Range(-0.1f, 0.1f);
        Vector2 popDirection = new Vector2(randomX, 1.0f).normalized;

        droppedItem.PopOut(popDirection, 4f);
    }
    //Function to get an template of drop

    private LootEntry GetItemToInstance()
    {
        float randF = Random.Range(0f,1f);

        foreach(var item in possibleDrops)
        {
            if(randF <= item.weight)
            {
                return item;
            }
        }
        return null;
    }

    //Function to get a rarity
    private ERarity GetRarity()
    {
        float randF = Random.Range(0f,1f);

        if(randF <= 0.5f)
        {
            return ERarity.Common;
        }
        else if(randF <= 0.85)
        {
            return ERarity.Rare;
        }
        else if(randF <= 0.95)
        {
            return ERarity.Epic;
        }
        else
        {
            return ERarity.Legendary;
        }
    }
}
