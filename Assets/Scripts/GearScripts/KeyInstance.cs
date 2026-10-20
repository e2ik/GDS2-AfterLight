using System;
using FMODUnity;

[Serializable]
public class KeyInstance
{
    public string InstanceGUID;
    public string InstItemID;

    public int PickupOrder;
    public bool IsNew = true;

    public EventReference pickupSound;
}