using UnityEngine;

[CreateAssetMenu(fileName = "GambleModifierGemTemplate", menuName = "SecondaryTemplates/GambleModifierGemTemplate")]
public class GambleModifierGemTemplate : SecondaryGemBehaviourDefinition
{
    [System.Serializable]
    public class GambleTier
    {
        public float chance;
        public float minMult;
        public float maxMult;
    }

    [Header(" Max Damage Mult by Rarity")]
    [SerializeField] private RarityRange maxDamageMultByRarity = new RarityRange();

    [Header("Tiers")]
    [SerializeField] private GambleTier whiff = new GambleTier { chance = 15f, minMult = 0.3f, maxMult = 0.6f };
    [SerializeField] private GambleTier normal = new GambleTier { chance = 45f, minMult = 0.9f, maxMult = 1.1f };
    [SerializeField] private GambleTier nice = new GambleTier { chance = 30f, minMult = 1.2f, maxMult = 1.5f };
    [SerializeField] private GambleTier jackpot = new GambleTier { chance = 10f, minMult = 1.6f };

    public override PassiveType GetPassiveType(SecondaryGemInstance instance)
    {
        return PassiveType.DamageMod;
    }

    public override void Modify(ref AttackContext context, SecondaryGemInstance instance)
    {
        float total = Mathf.Max(0.0001f, whiff.chance + normal.chance + nice.chance + jackpot.chance);
        float pick = Random.value * total;
        float t = Random.value;

        ERollTier tier;
        GambleTier chosen;
        float tierStart;

        if (pick < whiff.chance)
        {
            tier = ERollTier.Whiff; chosen = whiff; tierStart = 0f;
        }
        else if (pick < whiff.chance + normal.chance)
        {
            tier = ERollTier.Normal; chosen = normal; tierStart = whiff.chance;
        }
        else if (pick < whiff.chance + normal.chance + nice.chance)
        {
            tier = ERollTier.Nice; chosen = nice; tierStart = whiff.chance + normal.chance;
        }
        else
        {
            tier = ERollTier.Jackpot; chosen = jackpot; tierStart = whiff.chance + normal.chance + nice.chance;
        }

        float maxMult = tier == ERollTier.Jackpot
            ? Mathf.Max(jackpot.minMult, instance.InstRolledMaxGambleMult)
            : chosen.maxMult;

        float adjustedMult = Mathf.Lerp(chosen.minMult, maxMult, t);
        context.BaseAttackDamage *= adjustedMult;

        context.HasRoll = true;
        context.RollTier = tier;
        context.RollQuality = (tierStart + t * chosen.chance) / total;

        // Debug.Log($"Rolled {tier} ({adjustedMult:F2}x). Dealing {context.BaseAttackDamage}.");
    }

    public override SecondaryGemInstance CreateInstance(ERarity rarity)
    {
        SecondaryGemInstance instance = base.CreateInstance(rarity);

        Vector2 range = maxDamageMultByRarity.GetRange(rarity);
        instance.InstRolledMaxGambleMult = Random.Range(range.x, range.y);

        return instance;
    }
}