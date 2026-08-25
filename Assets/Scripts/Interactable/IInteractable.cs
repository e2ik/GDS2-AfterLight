public interface IInteractable
{
    void Interact(Player player);
    string InteractionPrompt { get; }
    bool CanInteract { get; }
}