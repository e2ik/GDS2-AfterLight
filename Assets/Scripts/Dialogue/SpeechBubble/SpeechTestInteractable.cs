using UnityEngine;

public class SpeechTestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerSpeechData speech;

    public string InteractionPrompt => "Inspect";
    public bool CanInteract => true;
    public bool ShouldStopPlayerMovement => false;

    public void Interact(Player player)
    {
        PlayerSpeechBubble speechBubble = player.GetComponentInChildren<PlayerSpeechBubble>();
        if (speechBubble != null) speechBubble.Show(speech, this);
    }
}