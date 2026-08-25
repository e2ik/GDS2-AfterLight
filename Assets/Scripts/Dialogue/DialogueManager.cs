using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Image portraitImage;

    private DialogueData currentDialogue;
    private int currentLineIndex;
    private Player currentPlayer;

    public bool IsDialogueActive { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    public void StartDialogue(DialogueData dialogue, Player player)
    {
        if (dialogue == null || dialogue.Lines == null || dialogue.Lines.Length == 0)
        {
            Debug.LogWarning("[DialogueManager] Dialogue has no lines.");
            return;
        }

        currentDialogue = dialogue;
        currentLineIndex = 0;
        currentPlayer = player;
        IsDialogueActive = true;

        if (currentPlayer != null)
        {
            currentPlayer.Controller.MovementEnabled = false;
            currentPlayer.InteractionManager.InteractionEnabled = false;
        }

        dialoguePanel.SetActive(true);
        ShowCurrentLine();
    }

    public void NextLine()
    {
        if (!IsDialogueActive) return;

        currentLineIndex++;

        if (currentLineIndex >= currentDialogue.Lines.Length)
        {
            EndDialogue();
            return;
        }

        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        DialogueLine line = currentDialogue.Lines[currentLineIndex];

        speakerNameText.text = line.SpeakerName;
        dialogueText.text = line.Text;

        if (line.Portrait != null)
        {
            portraitImage.sprite = line.Portrait;
            portraitImage.gameObject.SetActive(true);
        }
        else
        {
            portraitImage.gameObject.SetActive(false);
        }
    }

    public void EndDialogue()
    {
        IsDialogueActive = false;

        dialoguePanel.SetActive(false);

        if (currentPlayer != null)
        {
            currentPlayer.Controller.MovementEnabled = true;
            currentPlayer.InteractionManager.InteractionEnabled = true;
        }

        currentDialogue = null;
        currentPlayer = null;
        currentLineIndex = 0;
    }
}