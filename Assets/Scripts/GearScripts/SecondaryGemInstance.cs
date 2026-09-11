using System;
using UnityEngine;

[Serializable]
public class SecondaryGemInstance
{
    public string InstTemplateID;
    public string InstanceGUID;
    public SGemType Type;
    public int PickupOrder;
    public ERarity Rarity;

    public int InstRolledDamageValue;
    public int InstRolledCritValue;
    public int InstRolledDotPercent; // whole number, e.g. 10 = 10%

    public float InstRolledChargeAmount;
    public float InstRolledReflectPercent; // 0 - 1f
}