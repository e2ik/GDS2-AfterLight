using UnityEngine;

public class NPCDialogue : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData[] conversations;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "Talk";
    [SerializeField] private bool repeatLastConversation = true; // enable interaction to show last dialogue. cannot interact with npc if disabled
    [SerializeField] private SpriteOutlineToggle outlineToggle;
    [SerializeField] private Collider2D promptCollider;

    private int conversationIndex = 0;
    private bool finishedAllConversations = false;

    public string InteractionPrompt => interactionPrompt;

    public bool ShouldStopPlayerMovement => false;
    public SpriteOutlineToggle OutlineToggle => outlineToggle;
    public Collider2D PromptCollider => promptCollider;

    public bool CanInteract
    {
        get
        {
            if (DialogueManager.Instance == null)
                return false;

            if (DialogueManager.Instance.IsDialogueActive)
                return false;

            if (conversations == null || conversations.Length == 0)
                return false;

            if (finishedAllConversations && !repeatLastConversation)
                return false;

            return true;
        }
    }

    private void Awake()
    {
        if (outlineToggle == null) outlineToggle = GetComponent<SpriteOutlineToggle>();
        if (promptCollider == null) promptCollider = GetComponent<Collider2D>();
    }

    public void Interact(Player player)
    {
        if (!CanInteract)
            return;

        // zero playermovement
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

        if (rb != null) rb.linearVelocity = Vector2.zero;

        DialogueData dialogue = conversations[conversationIndex];

        if (dialogue == null)
        {
            Debug.LogWarning($"[NPCDialogue] Missing dialogue.");
            return;
        }

        DialogueManager.Instance.StartDialogue(dialogue,player,this, true);
    }

    public void OnDialogueFinished()
    {
        if (conversations == null || conversations.Length == 0)
            return;

        if (conversationIndex < conversations.Length - 1)
        {
            conversationIndex++;
        }
        else
        {
            finishedAllConversations = true;
        }
    }
}