using UnityEngine;

public class NPCDialogue : MonoBehaviour, IInteractable
{
    [Header("Dialogue")]
    [SerializeField] private DialogueData[] conversations;

    [Header("Interaction")]
    [SerializeField] private string interactionPrompt = "Talk";
    [SerializeField] private bool repeatLastConversation = true; // enable interaction to show last dialogue. cannot interact with npc if disabled

    private int conversationIndex = 0;
    private bool finishedAllConversations = false;

    public string InteractionPrompt => interactionPrompt;

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

    public void Interact(Player player)
    {
        if (!CanInteract)
            return;

        DialogueData dialogue = conversations[conversationIndex];

        if (dialogue == null)
        {
            Debug.LogWarning(
                $"[NPCDialogue] Missing dialogue at index {conversationIndex} on {gameObject.name}."
            );

            return;
        }

        DialogueManager.Instance.StartDialogue(
            dialogue,
            player,
            this
        );
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