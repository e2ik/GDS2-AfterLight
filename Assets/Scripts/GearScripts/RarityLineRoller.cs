using System.Collections.Generic;
using UnityEngine;

public class RarityLineRoller
{
    private readonly RarityWeights customOdds;
    private readonly ERarity? fixedRarity;
    private readonly int anchorIndex = -1;
    private readonly int tiersBelow;
    private readonly List<ERarity> lines = new List<ERarity>();

    public IReadOnlyList<ERarity> Lines => lines;

    public RarityLineRoller(RarityWeights customOdds = null)
    {
        this.customOdds = customOdds;
    }

    private RarityLineRoller(ERarity fixedRarity)
    {
        this.fixedRarity = fixedRarity;
    }

    private RarityLineRoller(ERarity fixedRarity, int anchorIndex, int tiersBelow)
    {
        this.fixedRarity = fixedRarity;
        this.anchorIndex = Mathf.Max(0, anchorIndex);
        this.tiersBelow = Mathf.Max(0, tiersBelow);
    }

    public static RarityLineRoller Fixed(ERarity rarity) => new RarityLineRoller(rarity);

    public static RarityLineRoller Anchored(ERarity rarity, int anchorIndex, int tiersBelow) =>
        new RarityLineRoller(rarity, anchorIndex, tiersBelow);

    public ERarity Next()
    {
        ERarity lineRarity;

        if (fixedRarity.HasValue && anchorIndex >= 0 && lines.Count != anchorIndex)
        {
            int top = (int)fixedRarity.Value;
            int bottom = Mathf.Max((int)ERarity.Common, top - tiersBelow);
            lineRarity = (ERarity)Random.Range(bottom, top + 1);
        }
        else
        {
            lineRarity = fixedRarity ?? RarityWeights.Roll(customOdds != null, customOdds);
        }

        lines.Add(lineRarity);
        return lineRarity;
    }

    public float RollValue(RarityRange range)
    {
        Vector2 minMax = range.GetRange(Next());
        return Random.Range(minMax.x, minMax.y);
    }

    public ERarity Final
    {
        get
        {
            if (fixedRarity.HasValue) return fixedRarity.Value;
            if (lines.Count == 0) return ERarity.Common;

            float total = 0f;
            foreach (ERarity line in lines) total += (int)line;

            int tier = Mathf.FloorToInt(total / lines.Count + 0.5f);
            return (ERarity)Mathf.Clamp(tier, (int)ERarity.Common, (int)ERarity.Legendary);
        }
    }
}