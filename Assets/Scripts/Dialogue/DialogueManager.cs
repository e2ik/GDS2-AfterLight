using System.Collections;
using FMODUnity;
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
    [SerializeField] private Image characterPortraitRight;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject nextDialogueIndicator;
    [SerializeField] private DialogueEffects dialogueEffects;

    [Header("Portrait Sides")]
    [SerializeField] private Object playerSpeaker;
    [SerializeField] private bool defaultPlayerOnLeft = true;
    [SerializeField] private bool flipRightPortrait = true;
    [SerializeField] private bool hideInactivePortrait = false;
    [SerializeField] private Color activePortraitColor = Color.white;
    [SerializeField] private Color inactivePortraitColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Dialogue Box Animation")]
    [SerializeField] private RectTransform dialoguePanelRect;
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private float slideOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float slideOffscreenMargin = 20f;

    [Header("Hold To Skip")]
    [SerializeField] private float holdSkipDuration = 1.5f;
    [SerializeField] private float holdSkipAppearDelay = 0.25f;
    [SerializeField] private GameObject holdSkipUI;
    [SerializeField] private Image holdSkipFill;
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
    private bool playerOnLeft = true;
    private bool waitingForInitialInteractRelease;
    public bool IsDialogueActive { get; private set; }
    public NPCDialogue CurrentNPC => currentNPC;

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

    private void OnDisable()
    {
        if (!IsDialogueActive) return;

        typingCoroutine = null;
        slideCoroutine = null;
        isTyping = false;

        FinishDialogueState();
    }

    private void Update()
    {
        if (!IsDialogueActive || inputLocked || currentPlayer == null)
            return;

        InputAction interactAction = currentPlayer.Input.actions["Interact"];

        if (interactAction == null)
            return;

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

            UpdateHoldSkipFill();

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

    // startedByInteract = true to start dialogue by interact, false to trigger by an event
    public void StartDialogue(DialogueData dialogue, Player player = null, NPCDialogue npc = null, bool startedByInteract = false)
    {
        if (IsDialogueActive)
            return;

        if (dialogue == null || dialogue.Lines == null || dialogue.Lines.Length == 0)
        {
            Debug.LogWarning("[DialogueManager] dialogue is empty");
            return;
        }

        currentDialogue = dialogue;
        currentPlayer = player;
        currentNPC = npc;
        currentLineIndex = 0;

        IsDialogueActive = true;
        inputLocked = true;
        waitingForInitialInteractRelease = startedByInteract;

        if (currentPlayer != null)
        {
            currentPlayer.Controller.InputEnabled = false;
            currentPlayer.InteractionManager.SetInteractionBlocked(true);
        }

        ResetHoldSkip();
        playerOnLeft = DeterminePlayerOnLeft();
        ResetPortraits();
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

        if (holdSkipFill != null)
        {
            holdSkipFill.fillAmount = 0f;
        }
    }

    private void UpdateHoldSkipFill()
    {
        if (holdSkipFill == null)
            return;

        float fillDuration = holdSkipDuration - holdSkipAppearDelay;
        float progress = fillDuration > 0f
            ? (interactHoldTime - holdSkipAppearDelay) / fillDuration
            : 1f;

        holdSkipFill.fillAmount = Mathf.Clamp01(progress);
    }

    private void ShowCurrentLine()
    {
        DialogueLine line = currentDialogue.Lines[currentLineIndex];

        if (dialogueEffects != null) dialogueEffects.PlayEffect(line.Effect);

        if (line.Speaker != null)
        {
            characterNameText.text = line.Speaker.CharacterName;
            ShowSpeakerPortrait(line.Speaker, line.Speaker.Portrait);
        }
        else
        {
            characterNameText.text = "";
            SetPortraitInactive(characterPortrait);
            SetPortraitInactive(characterPortraitRight);
        }

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

    private bool DeterminePlayerOnLeft()
    {
        if (currentPlayer == null || currentNPC == null) return defaultPlayerOnLeft;

        float playerX = currentPlayer.transform.position.x;
        float npcX = currentNPC.transform.position.x;
        if (Mathf.Approximately(playerX, npcX)) return defaultPlayerOnLeft;

        return playerX < npcX;
    }

    private void ResetPortraits()
    {
        if (characterPortrait != null) characterPortrait.gameObject.SetActive(false);

        if (characterPortraitRight != null)
        {
            characterPortraitRight.gameObject.SetActive(false);

            Vector3 scale = characterPortraitRight.rectTransform.localScale;
            scale.x = Mathf.Abs(scale.x) * (flipRightPortrait ? -1f : 1f);
            characterPortraitRight.rectTransform.localScale = scale;
        }
    }

    private void ShowSpeakerPortrait(Object speaker, Sprite portrait)
    {
        bool isPlayer = playerSpeaker != null && speaker == playerSpeaker;
        bool onLeft = characterPortraitRight == null || (isPlayer ? playerOnLeft : !playerOnLeft);

        Image active = onLeft ? characterPortrait : characterPortraitRight;
        Image other = onLeft ? characterPortraitRight : characterPortrait;

        if (active != null)
        {
            if (portrait != null)
            {
                active.sprite = portrait;
                active.color = activePortraitColor;
                active.gameObject.SetActive(true);
            }
            else
            {
                active.gameObject.SetActive(false);
            }
        }

        SetPortraitInactive(other);
    }

    private void SetPortraitInactive(Image portrait)
    {
        if (portrait == null || !portrait.gameObject.activeSelf) return;

        if (hideInactivePortrait) portrait.gameObject.SetActive(false);
        else portrait.color = inactivePortraitColor;
    }

    private IEnumerator TypeLine(DialogueLine line)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char character in line.Text)
        {
            dialogueText.text += character;

            PlayTypingSound(line);

            float textSpeed = line.TextSpeed;

            if (line.Effect == DialogueEffect.Angry) textSpeed *= 0.7f;

            yield return new WaitForSecondsRealtime(textSpeed);
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
        if (line.Speaker == null)
            return;

        if (line.Speaker.TypingSound.IsNull)
            return;

        AudioManager.PlaySFX(line.Speaker.TypingSound);
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
        dialoguePanelRect.anchoredPosition = dialoguePanelRestPosition;

        Vector2 startPosition =
            dialoguePanelRestPosition - new Vector2(0f, GetOffscreenOffset());

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

    private float GetOffscreenOffset()
    {
        Canvas canvas = dialoguePanelRect.GetComponentInParent<Canvas>();
        if (canvas == null) return 0f;

        RectTransform canvasRect = canvas.rootCanvas.transform as RectTransform;
        if (canvasRect == null) return 0f;

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, dialoguePanelRect);
        float offsetInCanvas = bounds.max.y - canvasRect.rect.yMin + slideOffscreenMargin;

        Transform parent = dialoguePanelRect.parent;
        float parentScale = parent != null ? parent.lossyScale.y : 1f;
        float ratio = Mathf.Approximately(parentScale, 0f) ? 1f : canvasRect.lossyScale.y / parentScale;

        return Mathf.Max(0f, offsetInCanvas * ratio);
    }

    private void PlaySlideOut()
    {
        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }

        if (dialoguePanelRect == null || slideOutDuration <= 0f || !dialoguePanel.activeInHierarchy)
        {
            dialoguePanel.SetActive(false);
            return;
        }

        slideCoroutine = StartCoroutine(SlideOutRoutine());
    }

    private IEnumerator SlideOutRoutine()
    {
        Vector2 startPosition = dialoguePanelRect.anchoredPosition;
        Vector2 endPosition = startPosition - new Vector2(0f, GetOffscreenOffset());

        float elapsed = 0f;

        while (elapsed < slideOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / slideOutDuration);
            dialoguePanelRect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
            yield return null;
        }

        dialoguePanelRect.anchoredPosition = dialoguePanelRestPosition;
        dialoguePanel.SetActive(false);
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
        if (dialogueEffects != null) dialogueEffects.StopEffects(); // stop effects
        PlaySlideOut();

        FinishDialogueState();
    }

    private void FinishDialogueState()
    {
        if (currentPlayer != null)
        {
            currentPlayer.Controller.InputEnabled = true;
            currentPlayer.InteractionManager.SetInteractionBlocked(false);
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