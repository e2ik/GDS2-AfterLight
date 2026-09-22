using UnityEngine;

public class SpeechTestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerSpeechData speech;
    [SerializeField] private SpriteOutlineToggle outlineToggle;

    public string InteractionPrompt => "Inspect";
    public bool CanInteract => true;
    public bool ShouldStopPlayerMovement => false;
    public SpriteOutlineToggle OutlineToggle => outlineToggle;

    private void Awake()
    {
        if (outlineToggle == null) outlineToggle = GetComponent<SpriteOutlineToggle>();
    }

    public void Interact(Player player)
    {
        PlayerSpeechBubble speechBubble = player.GetComponentInChildren<PlayerSpeechBubble>();
        if (speechBubble != null) speechBubble.Show(speech, this);
    }
}