using UnityEngine;

public interface IInteractable
{
    void Interact(Player player);
    string InteractionPrompt { get; }
    bool CanInteract { get; }
    bool ShouldStopPlayerMovement { get; }
    SpriteOutlineToggle OutlineToggle { get; }
    Collider2D PromptCollider { get; }
}