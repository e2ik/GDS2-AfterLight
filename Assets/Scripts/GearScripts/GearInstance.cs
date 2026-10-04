using System;
using System.Collections.Generic;

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

    public List<ERarity> LineRarities = new List<ERarity>();

    public int PickupOrder;
    public bool IsNew = true;
}