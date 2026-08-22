using UnityEngine;

public class FastTravelPoint : MonoBehaviour, IInteractable
{
    [Header("Fast Travel Config")]
    [SerializeField] private FastTravelNodeSO nodeData;
    [SerializeField] private WorldMapStateSO worldMapState;

    [Header("Interaction Settings")]
    [SerializeField] private bool canBeInteractedWith = true;

    public bool CanInteract => canBeInteractedWith;

    public string InteractionPrompt => worldMapState.IsUnlocked(nodeData) 
        ? $"Travel from {nodeData.displayName}" 
        : $"Unlock {nodeData.displayName}";

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        if (!worldMapState.IsUnlocked(nodeData))
        {
            worldMapState.UnlockNode(nodeData);
        }

        if (MapUIManager.Instance != null)
        {
            MapUIManager.Instance.OpenMap(nodeData);
        }
        else
        {
            Debug.LogError("MapUIManager Instance is missing from the scene!");
        }
    }
}