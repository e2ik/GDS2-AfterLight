using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Image characterPortrait;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject nextDialogueIndicator;

    [Header("Typewriter")]
    [SerializeField] private AudioSource dialogueAudioSource;

    [Header("Dialogue Box Animation")]
    [SerializeField] private RectTransform dialoguePanelRect;
    [SerializeField] private float slideOffsetY = -200f;
    [SerializeField] private float slideDuration = 0.25f;

    [Header("Hold To Skip")]
    [SerializeField] private float holdSkipDuration = 1.5f;
    [SerializeField] private float holdSkipAppearDelay = 0.25f;
    [SerializeField] private GameObject holdSkipUI;
    private float interactHoldTime;
    private bool isHoldingInteract;
    private bool holdSkipTriggered;

    private DialogueData currentDialogue;
    private NPCDialogue currentNPC;
    private Player currentPlayer;
    private int currentLineIndex;
    private Coroutine typingCoroutine;
    private Coroutine slideCoroutine;
    private bool isTyping;
    private bool inputLocked;
    private Vector2 dialoguePanelRestPosition;
    private bool waitingForInitialInteractRelease;
    public bool IsDialogueActive { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dialoguePanelRect == null && dialoguePanel != null)
        {
            dialoguePanelRect = dialoguePanel.GetComponent<RectTransform>();
        }

        if (dialoguePanelRect != null)
        {
            dialoguePanelRestPosition = dialoguePanelRect.anchoredPosition;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsDialogueActive || inputLocked || currentPlayer == null)
            return;

        InputAction interactAction = currentPlayer.Input.actions["Interact"];

        if (interactAction == null)
            return;

        // Ignore the release from the button press that originally opened dialogue.
        if (waitingForInitialInteractRelease)
        {
            if (interactAction.WasReleasedThisFrame())
            {
                waitingForInitialInteractRelease = false;
                ResetHoldSkip();
            }

            return;
        }

        if (interactAction.WasPressedThisFrame())
        {
            isHoldingInteract = true;
            holdSkipTriggered = false;
            interactHoldTime = 0f;
        }

        if (isHoldingInteract && interactAction.IsPressed())
        {
            interactHoldTime += Time.unscaledDeltaTime;

            if (holdSkipUI != null &&
                interactHoldTime >= holdSkipAppearDelay)
            {
                holdSkipUI.SetActive(true);
            }

            if (!holdSkipTriggered &&
                interactHoldTime >= holdSkipDuration)
            {
                holdSkipTriggered = true;
                SkipConversation();
                return;
            }
        }

        if (interactAction.WasReleasedThisFrame() && isHoldingInteract)
        {
            if (!holdSkipTriggered)
            {
                HandleInteractInput();
            }

            ResetHoldSkip();
        }
    }

    public void StartDialogue(DialogueData dialogue, Player player = null, NPCDialogue npc = null)
    {
        if (IsDialogueActive)
            return;

        if (dialogue == null || dialogue.Lines == null || dialogue.Lines.Length == 0)
        {
            Debug.LogWarning("[DialogueManager] Cannot start empty dialogue.");
            return;
        }

        currentDialogue = dialogue;
        currentPlayer = player;
        currentNPC = npc;
        currentLineIndex = 0;

        IsDialogueActive = true;
        inputLocked = true;
        waitingForInitialInteractRelease = true;

        if (currentPlayer != null)
        {
            currentPlayer.Controller.MovementEnabled = false;
            currentPlayer.InteractionManager.InteractionEnabled = false;
        }

        ResetHoldSkip();
        dialoguePanel.SetActive(true);

        PlaySlideIn();
        ShowCurrentLine();

        StartCoroutine(UnlockInputNextFrame());
    }

    private IEnumerator UnlockInputNextFrame()
    {
        yield return null;
        inputLocked = false;
    }

    private void HandleInteractInput()
    {
        if (isTyping)
        {
            RevealCurrentLine();
            return;
        }

        bool isLastLine =
            currentLineIndex >= currentDialogue.Lines.Length - 1;

        if (isLastLine)
        {
            EndDialogue();
            return;
        }

        currentLineIndex++;
        ShowCurrentLine();
    }

    private void SkipConversation()
    {
        if (!IsDialogueActive)
            return;

        EndDialogue();
    }

    private void ResetHoldSkip()
    {
        interactHoldTime = 0f;
        isHoldingInteract = false;
        holdSkipTriggered = false;

        if (holdSkipUI != null)
        {
            holdSkipUI.SetActive(false);
        }
    }

    private void ShowCurrentLine()
    {
        DialogueLine line = currentDialogue.Lines[currentLineIndex];

        characterNameText.text = line.SpeakerName;

        if (line.Portrait != null)
        {
            characterPortrait.sprite = line.Portrait;
            characterPortrait.gameObject.SetActive(true);
        }
        else
        {
            characterPortrait.gameObject.SetActive(false);
        }

        // Keep indicator visible while the line is typing.
        if (nextDialogueIndicator != null)
        {
            nextDialogueIndicator.SetActive(true);
        }

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        typingCoroutine = StartCoroutine(TypeLine(line));
    }

    private IEnumerator TypeLine(DialogueLine line)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char character in line.Text)
        {
            dialogueText.text += character;

            PlayTypingSound(line);

            yield return new WaitForSeconds(currentDialogue.TextSpeed);
        }

        isTyping = false;
        typingCoroutine = null;

        UpdateContinueIndicator();
    }

    private void RevealCurrentLine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialogueText.text =
            currentDialogue.Lines[currentLineIndex].Text;

        isTyping = false;

        UpdateContinueIndicator();
    }

    private void UpdateContinueIndicator()
    {
        if (nextDialogueIndicator == null)
            return;

        bool isLastLine =
            currentLineIndex >= currentDialogue.Lines.Length - 1;

        nextDialogueIndicator.SetActive(!isLastLine);
    }

    private void PlayTypingSound(DialogueLine line)
    {
        if (dialogueAudioSource == null)
            return;

        if (line.TypingSound == null)
            return;

        dialogueAudioSource.PlayOneShot(line.TypingSound);
    }

    private void PlaySlideIn()
    {
        if (dialoguePanelRect == null)
            return;

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
        }

        slideCoroutine = StartCoroutine(SlideInRoutine());
    }

    private IEnumerator SlideInRoutine()
    {
        Vector2 startPosition =
            dialoguePanelRestPosition + new Vector2(0f, slideOffsetY);

        dialoguePanelRect.anchoredPosition = startPosition;

        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / slideDuration);

            dialoguePanelRect.anchoredPosition =
                Vector2.Lerp(
                    startPosition,
                    dialoguePanelRestPosition,
                    t
                );

            yield return null;
        }

        dialoguePanelRect.anchoredPosition =
            dialoguePanelRestPosition;

        slideCoroutine = null;
    }

    public void EndDialogue()
    {
        if (!IsDialogueActive)
            return;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
        inputLocked = true;

        ResetHoldSkip();
        dialoguePanel.SetActive(false);

        if (currentPlayer != null)
        {
            currentPlayer.Controller.MovementEnabled = true;
            currentPlayer.InteractionManager.InteractionEnabled = true;
        }

        if (currentNPC != null)
        {
            currentNPC.OnDialogueFinished();
        }

        currentDialogue = null;
        currentPlayer = null;
        currentNPC = null;
        currentLineIndex = 0;

        IsDialogueActive = false;
        inputLocked = false;
    }
}