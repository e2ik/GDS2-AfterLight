using UnityEngine;

public struct AttackContext
{
    public GameObject PlayerGO { get; }
    public PlayerController PlayerController { get; }
    public MonoBehaviour Runner;

    public Vector2 OriginPoint;
    public float BaseAttackDamage;
    public float BaseAttackCrit;
    public float BaseAttackRange;

    public EDamageType DamageType;

    public bool AppliesDot;
    public float DotDamagePercent;
    public float DotTickInterval;
    public float DotDuration;

    public bool ChargesSkillMeter;
    public float ChargeAmount;

    public float SkillModifierBonus;

    public bool HasRoll;
    public float RollQuality;
    public ERollTier RollTier;
}

public enum EDamageType
{
    Base,
    Skill,
    Dot,
    Reflect
}

public enum ERollTier
{
    None,
    Whiff,
    Normal,
    Nice,
    Jackpot
}