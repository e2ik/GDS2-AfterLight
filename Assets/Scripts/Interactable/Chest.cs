using UnityEngine;

public class Chest : MonoBehaviour, IInteractable
{
    private bool isOpened = false;
    [SerializeField] private string chestID;
    public string InteractionPrompt => "Open Chest";
    public bool CanInteract => !isOpened;

    public SecondaryGemBehaviourDefinition gemLootTemplate;

    private void Start()
    {
        isOpened = SaveManager.Instance.IsChestOpened(chestID);
        if (isOpened)
        {
            ApplyOpenedVisualState();
        }
    }

    public void Interact(Player player)
    {
        isOpened = true;
        SecondaryGemInstance gemLoot = gemLootTemplate.CreateInstance(ERarity.Common);
        player.GetComponent<PlayerInventoryManager>().AddItemToInventory(gemLoot);
        player.GetComponent<PlayerController>().secondaryGem = gemLoot;
        Debug.Log("Chest opened! Player received loot.");

        SaveManager.Instance.MarkChestOpened(chestID);
        ApplyOpenedVisualState();
    }

    private void ApplyOpenedVisualState()
    {
        // swap sprite/animation to show already-looted state
    }
}