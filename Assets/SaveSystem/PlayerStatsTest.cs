using UnityEngine;
using System.Collections.Generic;

public class PlayerStatsTest : MonoBehaviour
{
    // base stats
    public int baseAttack;
    public int baseDefense;
    public int baseHumanity;

    // better to separate to prevent issues
    public int attack;
    public int defense;
    public int humanity;

    public void RecalculateStats(List<ItemInstance> equippedItems)
    {
        attack = baseAttack;
        defense = baseDefense;
        humanity = baseHumanity;

        foreach (var item in equippedItems)
        {
            foreach (var stat in item.rolledStats)
            {
                switch (stat.statName)
                {
                    case "attack": attack += stat.value; break;
                    case "defense": defense += stat.value; break;
                    case "humanity": humanity += stat.value; break;
                }
            }
        }
    }

    public PlayerStatsSaveData ToSaveData()
    {
        return new PlayerStatsSaveData
        {
            attack = baseAttack,
            defense = baseDefense,
            humanity = baseHumanity
        };
    }

    public void LoadFromSaveData(PlayerStatsSaveData data)
    {
        baseAttack = data.attack;
        baseDefense = data.defense;
        baseHumanity = data.humanity;
    }
}
