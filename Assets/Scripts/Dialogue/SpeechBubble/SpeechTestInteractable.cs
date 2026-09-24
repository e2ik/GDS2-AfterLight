using UnityEngine;

public class SpeechTestInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private PlayerSpeechData speech;
    [SerializeField] private SpriteOutlineToggle outlineToggle;
    [SerializeField] private Collider2D promptCollider;

    public string InteractionPrompt => "Inspect";
    public bool CanInteract => true;
    public bool ShouldStopPlayerMovement => false;
    public SpriteOutlineToggle OutlineToggle => outlineToggle;
    public Collider2D PromptCollider => promptCollider;

    private void Awake()
    {
        if (outlineToggle == null) outlineToggle = GetComponent<SpriteOutlineToggle>();
        if (promptCollider == null) promptCollider = GetComponent<Collider2D>();
    }

    public void Interact(Player player)
    {
        PlayerSpeechBubble speechBubble = player.GetComponentInChildren<PlayerSpeechBubble>();
        if (speechBubble != null) speechBubble.Show(speech, this);
    }
}