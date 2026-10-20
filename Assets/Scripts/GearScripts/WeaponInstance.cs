using System.Collections.Generic;
using FMODUnity;

[System.Serializable]
public class WeaponInstance
{
    public string InstanceGUID;
    public string InstTemplateID;
    public ERarity Rarity;
    public float InstRolledDamage;
    public float InstRolledRange;
    public float InstRolledCrit;
    public float InstRolledAttack;
    public float InstRolledDefense;
    public List<ERarity> LineRarities = new List<ERarity>();
    public int PickupOrder;
    public bool IsNew = true;
    
    public EventReference pickupSound;
}