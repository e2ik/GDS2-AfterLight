using System.Collections.Generic;

public class LoreSetDisplayInfo
{
    public string SetID;
    public LoreItemInstance RepresentativeInstance;
    public List<LoreItemInstance> OwnedInstances = new List<LoreItemInstance>();
    public int TotalPieces = 1;

    public int OwnedCount => OwnedInstances.Count;
    public bool IsComplete => OwnedCount >= TotalPieces;
}