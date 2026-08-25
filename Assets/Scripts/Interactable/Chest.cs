using UnityEngine;

public class Chest : MonoBehaviour, IInteractable
{
    private bool isOpened = false;

    public string InteractionPrompt => "Open Chest";
    public bool CanInteract => !isOpened;

    public SecondaryGemBehaviourDefinition gemLootTemplate;

    public void Interact(Player player)
    {
        isOpened = true;
        SecondaryGemInstance gemLoot = gemLootTemplate.CreateInstance(ERarity.Common);
        player.GetComponent<PlayerInventoryManager>().AddItemToInventory(gemLoot);
        player.GetComponent<PlayerController>().secondaryGem = gemLoot;
        Debug.Log("Chest opened! Player received loot.");
    }
}