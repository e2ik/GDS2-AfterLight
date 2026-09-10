using System;
using UnityEngine;

[Serializable]
public class SecondaryGemInstance
{
    public string InstTemplateID;
    public int InstRolledDamageValue;
    public int InstRolledCritValue;
    public int InstRolledDotPercent; // whole number, e.g. 10 = 10%
    public string InstanceGUID;
}
