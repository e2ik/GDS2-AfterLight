using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldMapState", menuName = "World/Map State Tracker")]
public class WorldMapStateSO : ScriptableObject
{
    public event Action<FastTravelNodeSO> OnNodeUnlocked;
    
    [SerializeField] private List<FastTravelNodeSO> unlockedNodes = new List<FastTravelNodeSO>();
    
    public IReadOnlyList<FastTravelNodeSO> UnlockedNodes => unlockedNodes;

    public void UnlockNode(FastTravelNodeSO node)
    {
        if (!unlockedNodes.Contains(node))
        {
            unlockedNodes.Add(node);
            OnNodeUnlocked?.Invoke(node);
        }
    }

    public bool IsUnlocked(FastTravelNodeSO node) => unlockedNodes.Contains(node);
}