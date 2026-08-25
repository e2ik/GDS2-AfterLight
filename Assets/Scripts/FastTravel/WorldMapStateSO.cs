using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

    public List<string> ToSaveIDs()
    {
        return unlockedNodes.Where(n => n != null).Select(n => n.nodeID).ToList();
    }

    public void LoadFromSaveIDs(List<string> ids)
    {
        unlockedNodes.Clear();
        if (ids == null) return;

        foreach (var id in ids)
        {
            var node = FastTravelNodeResolver.GetByID(id);
            if (node != null)
                unlockedNodes.Add(node);
            else
                Debug.LogWarning($"[WorldMapState] Unknown fast travel node ID '{id}' in save data.");
        }
    }

    public void ResetState()
    {
        unlockedNodes.Clear();
    }
}