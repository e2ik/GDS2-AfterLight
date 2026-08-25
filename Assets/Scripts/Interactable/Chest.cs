using UnityEngine;

public class Chest : MonoBehaviour, IInteractable
{
    private bool isOpened = false;

    public string InteractionPrompt => "Open Chest";
    public bool CanInteract => !isOpened;

    public void Interact(Player player)
    {
        isOpened = true;
        Debug.Log("Chest opened! Player received loot.");
    }
}