using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "WorldMapState", menuName = "World/Map State Tracker")]
public class WorldMapStateSO : ScriptableObject
{
    public event Action<FastTravelNodeSO> OnNodeUnlocked;
    public event Action OnStateLoaded;
    public event Action OnStateReset;
    
    [SerializeField] private List<FastTravelNodeSO> unlockedNodes = new List<FastTravelNodeSO>();
    
    public IReadOnlyList<FastTravelNodeSO> UnlockedNodes => unlockedNodes;

    // Editor problems
    private void OnEnable()
    {
        #if UNITY_EDITOR
        ResetState();
        #endif
    }

    public void UnlockNode(FastTravelNodeSO node)
    {
        if (node != null && !unlockedNodes.Contains(node))
        {
            unlockedNodes.Add(node);
            OnNodeUnlocked?.Invoke(node);
        }
    }

    public bool IsUnlocked(FastTravelNodeSO node) => node != null && unlockedNodes.Contains(node);

    public List<string> ToSaveIDs()
    {
        return unlockedNodes.Where(n => n != null).Select(n => n.nodeID).ToList();
    }

    public void LoadFromSaveIDs(List<string> ids)
    {
        unlockedNodes.Clear();

        if (ids != null && ids.Count > 0)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;

                var node = FastTravelNodeResolver.GetByID(id);
                if (node != null)
                {
                    if (!unlockedNodes.Contains(node))
                    {
                        unlockedNodes.Add(node);
                    }
                }
                else
                {
                    Debug.LogWarning($"[WorldMapState] Could not resolve FastTravelNodeSO for ID '{id}'");
                }
            }
        }

        Debug.Log($"[WorldMapStateSO] Successfully loaded {unlockedNodes.Count} nodes.");
        OnStateLoaded?.Invoke();
    }

    public void ResetState()
    {
        unlockedNodes.Clear();
        OnStateReset?.Invoke();
    }
}