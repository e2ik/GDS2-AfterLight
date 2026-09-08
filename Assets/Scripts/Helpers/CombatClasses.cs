using UnityEngine;

public struct AttackContext{
    //Immutables
    public GameObject PlayerGO{get;}
    public PlayerController PlayerController{get;}
    //This is what to use for running coroutines in the execute function of Primary Behviour.
    public MonoBehaviour Runner;
    //Mutables
    public Vector2 OriginPoint;
    public float BaseAttackDamage;
    public float BaseAttackCrit;
    public float BaseAttackRange;

    public EDamageType DamageType;
}

public enum EDamageType
{
    Base,
    Fire,
    Poison,
    Kinetic
}