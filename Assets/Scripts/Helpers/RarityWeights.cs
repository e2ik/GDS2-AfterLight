using System;
using UnityEngine;

[Serializable]
public class RarityWeights
{
    [Min(0f)] public float common = 50f;
    [Min(0f)] public float rare = 35f;
    [Min(0f)] public float epic = 10f;
    [Min(0f)] public float legendary = 5f;

    private static readonly RarityWeights Fallback = new RarityWeights();

    public ERarity Roll()
    {
        float total = common + rare + epic + legendary;
        if (total <= 0f) return ERarity.Common;

        float pick = UnityEngine.Random.value * total;

        if ((pick -= common) < 0f) return ERarity.Common;
        if ((pick -= rare) < 0f) return ERarity.Rare;
        if ((pick -= epic) < 0f) return ERarity.Epic;
        return ERarity.Legendary;
    }

    public static ERarity RollDefault()
    {
        RarityWeights defaults = GameManager.Instance != null ? GameManager.Instance.DefaultRarityOdds : null;
        return (defaults ?? Fallback).Roll();
    }

    public static ERarity Roll(bool useCustom, RarityWeights custom)
    {
        return useCustom && custom != null ? custom.Roll() : RollDefault();
    }

    public static bool UsesRarity(InventoryItemBase item)
    {
        return item is WeaponDefinition
            || item is GearDefinition
            || item is SecondaryGemBehaviourDefinition;
    }
}