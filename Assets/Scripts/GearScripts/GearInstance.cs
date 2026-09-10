using System;

[Serializable]
public class GearInstance
{
    public string InstanceGUID;
    public string InstTemplateID;
    public ERarity Rarity;

    public float InstBonusAttack;
    public float InstBonusDefense;
    public float InstBonusHumanity;
    public float InstBonusCrit;

    public int PickupOrder;
}