using System.Collections.Generic;
using UnityEngine;

public static class FastTravelNodeResolver
{
    private static Dictionary<string, FastTravelNodeSO> _cache;

    public static FastTravelNodeSO GetByID(string nodeID)
    {
        if (_cache == null) BuildCache();
        _cache.TryGetValue(nodeID, out var node);
        return node;
    }

    private static void BuildCache()
    {
        var allNodes = Resources.LoadAll<FastTravelNodeSO>("FastTravel");

        _cache = new Dictionary<string, FastTravelNodeSO>();
        foreach (var node in allNodes)
        {
            if (string.IsNullOrEmpty(node.nodeID))
            {
                Debug.LogError($"[FastTravelNodeResolver] '{node.name}' has no nodeID.");
                continue;
            }
            if (!_cache.TryAdd(node.nodeID, node))
                Debug.LogError($"[FastTravelNodeResolver] Duplicate nodeID '{node.nodeID}' on '{node.name}'.");
        }
    }
}