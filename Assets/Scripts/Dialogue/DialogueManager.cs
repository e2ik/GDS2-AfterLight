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
    [SerializeField] private GameObject textboxFrameLeft;
    [SerializeField] private GameObject textboxFrameRight;
    [SerializeField] private TMP_Text characterNameTextRight;
    [SerializeField] private TMP_Text dialogueTextRight;
    [SerializeField] private Graphic textboxBackgroundLeft;
    [SerializeField] private Graphic textboxBackgroundRight;
    [SerializeField] private Graphic panelBackgroundLeft;
    [SerializeField] private Graphic panelBackgroundRight;
    [SerializeField, Min(0)] private int panelBackgroundDarkenSteps = 2;
    [SerializeField, Range(0f, 1f)] private float panelBackgroundDarkenStepAmount = 0.15f;
    [SerializeField] private bool tintNameWithPanelColor = true;
    [SerializeField, Min(0)] private int nameBrightenSteps = 2;
    [SerializeField, Range(0f, 1f)] private float nameBrightenStepAmount = 0.15f;
    [SerializeField] private GameObject nextDialogueIndicator;
    [SerializeField] private bool hideContinueIndicatorOnLastLine = false;
    [SerializeField] private GameObject continuePrompt;
    [SerializeField, Min(0f)] private float promptFadeInDuration = 0.1f;
    [SerializeField, Min(0f)] private float promptFadeOutDuration = 0.08f;
    [SerializeField] private DialogueEffects dialogueEffects;

    [Header("Portrait Sides")]
    [SerializeField] private Object playerSpeaker;
    [SerializeField] private bool defaultPlayerOnLeft = true;
    [SerializeField] private bool flipRightPortrait = true;
    [SerializeField] private bool hideInactivePortrait = false;
    [SerializeField] private Color activePortraitColor = Color.white;
    [SerializeField] private Color inactivePortraitColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField, Min(0f)] private float activePortraitScale = 1f;
    [SerializeField, Min(0f)] private float inactivePortraitScale = 0.9f;
    [SerializeField, Min(0f)] private float portraitScaleSpeed = 12f;

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
    private Coroutine promptFadeCoroutine;
    private CanvasGroup continuePromptGroup;
    private bool isTyping;
    private bool inputLocked;
    private Vector2 dialoguePanelRestPosition;
    private bool playerOnLeft = true;
    private bool textboxOnLeft = true;
    private TMP_Text activeNameText;
    private TMP_Text activeDialogueText;
    private Color textboxBackgroundLeftDefault = Color.white;
    private Color textboxBackgroundRightDefault = Color.white;
    private Color panelBackgroundLeftDefault = Color.white;
    private Color panelBackgroundRightDefault = Color.white;
    private Color nameTextLeftDefault = Color.white;
    private Color nameTextRightDefault = Color.white;
    private bool waitingForInitialInteractRelease;
    private Vector3 leftPortraitBaseScale = Vector3.one;
    private Vector3 rightPortraitBaseScale = Vector3.one;
    private float leftPortraitScale = 1f;
    private float rightPortraitScale = 1f;
    private float leftPortraitScaleTarget = 1f;
    private float rightPortraitScaleTarget = 1f;
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

        if (textboxBackgroundLeft != null) textboxBackgroundLeftDefault = textboxBackgroundLeft.color;
        if (textboxBackgroundRight != null) textboxBackgroundRightDefault = textboxBackgroundRight.color;
        if (panelBackgroundLeft != null) panelBackgroundLeftDefault = panelBackgroundLeft.color;
        if (panelBackgroundRight != null) panelBackgroundRightDefault = panelBackgroundRight.color;
        if (characterNameText != null) nameTextLeftDefault = characterNameText.color;
        if (characterNameTextRight != null) nameTextRightDefault = characterNameTextRight.color;

        if (characterPortrait != null) leftPortraitBaseScale = AbsScale(characterPortrait.rectTransform.localScale);
        if (characterPortraitRight != null) rightPortraitBaseScale = AbsScale(characterPortraitRight.rectTransform.localScale);

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
        promptFadeCoroutine = null;
        isTyping = false;

        FinishDialogueState();
    }

    private void Update()
    {
        UpdatePortraitScales();

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
        SetPromptVisible(false, true);
        textboxOnLeft = !playerOnLeft;
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

        SetTextboxSide(line.Speaker != null ? IsSpeakerOnLeft(line.Speaker) : textboxOnLeft);
        ApplyTextboxColor(line.Speaker);

        if (dialogueEffects != null)
        {
            GameObject frame = textboxOnLeft ? textboxFrameLeft : textboxFrameRight;
            Image speakerPortrait = null;
            if (line.Speaker != null && line.Speaker.Portrait != null)
                speakerPortrait = IsSpeakerOnLeft(line.Speaker) ? characterPortrait : characterPortraitRight;

            dialogueEffects.SetTargets(activeDialogueText,
                frame != null ? frame.transform as RectTransform : null,
                speakerPortrait != null ? speakerPortrait.rectTransform : null);
            dialogueEffects.PlayEffect(line.Effect);
        }

        if (line.Speaker != null)
        {
            if (activeNameText != null) activeNameText.text = line.Speaker.CharacterName;
            ShowSpeakerPortrait(line.Speaker, line.Speaker.Portrait);
        }
        else
        {
            if (activeNameText != null) activeNameText.text = "";
            SetPortraitInactive(characterPortrait);
            SetPortraitInactive(characterPortraitRight);
        }

        if (nextDialogueIndicator != null)
        {
            nextDialogueIndicator.SetActive(true);
        }

        SetPromptVisible(false);

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
        if (characterPortraitRight != null) characterPortraitRight.gameObject.SetActive(false);

        SetPortraitScaleTarget(characterPortrait, inactivePortraitScale, true);
        SetPortraitScaleTarget(characterPortraitRight, inactivePortraitScale, true);
    }

    private static Vector3 AbsScale(Vector3 scale)
    {
        return new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    private void SetPortraitScaleTarget(Image portrait, float target, bool snap = false)
    {
        if (portrait == null) return;

        if (portrait == characterPortraitRight)
        {
            rightPortraitScaleTarget = target;
            if (snap) rightPortraitScale = target;
        }
        else
        {
            leftPortraitScaleTarget = target;
            if (snap) leftPortraitScale = target;
        }

        ApplyPortraitScales();
    }

    private void UpdatePortraitScales()
    {
        float t = 1f - Mathf.Exp(-portraitScaleSpeed * Time.unscaledDeltaTime);
        leftPortraitScale = Mathf.Lerp(leftPortraitScale, leftPortraitScaleTarget, t);
        rightPortraitScale = Mathf.Lerp(rightPortraitScale, rightPortraitScaleTarget, t);
        ApplyPortraitScales();
    }

    private void ApplyPortraitScales()
    {
        if (characterPortrait != null)
            characterPortrait.rectTransform.localScale = leftPortraitBaseScale * leftPortraitScale;

        if (characterPortraitRight != null)
        {
            Vector3 scale = rightPortraitBaseScale * rightPortraitScale;
            if (flipRightPortrait) scale.x = -scale.x;
            characterPortraitRight.rectTransform.localScale = scale;
        }
    }

    private bool IsSpeakerOnLeft(Object speaker)
    {
        bool isPlayer = playerSpeaker != null && speaker == playerSpeaker;
        return characterPortraitRight == null || (isPlayer ? playerOnLeft : !playerOnLeft);
    }

    private bool HasRightTextbox => textboxFrameRight != null && dialogueTextRight != null;

    private void SetTextboxSide(bool onLeft)
    {
        if (!HasRightTextbox) onLeft = true;
        textboxOnLeft = onLeft;

        if (textboxFrameLeft != null) textboxFrameLeft.SetActive(onLeft);
        if (textboxFrameRight != null) textboxFrameRight.SetActive(!onLeft);

        TMP_Text nextName = onLeft || characterNameTextRight == null ? characterNameText : characterNameTextRight;
        TMP_Text nextDialogue = onLeft ? dialogueText : dialogueTextRight;

        if (activeNameText != null && activeNameText != nextName) activeNameText.text = "";
        if (activeDialogueText != null && activeDialogueText != nextDialogue) activeDialogueText.text = "";

        activeNameText = nextName;
        activeDialogueText = nextDialogue;
    }

    private void ApplyTextboxColor(CharacterData speaker)
    {
        bool usePanelColor = speaker != null && speaker.UsePanelColor;

        Graphic background = textboxOnLeft ? textboxBackgroundLeft : textboxBackgroundRight;
        if (background != null)
        {
            Color fallback = textboxOnLeft ? textboxBackgroundLeftDefault : textboxBackgroundRightDefault;
            background.color = usePanelColor ? speaker.PanelColor : fallback;
        }

        Graphic panelBackground = textboxOnLeft ? panelBackgroundLeft : panelBackgroundRight;
        if (panelBackground != null)
        {
            Color panelFallback = textboxOnLeft ? panelBackgroundLeftDefault : panelBackgroundRightDefault;
            if (usePanelColor)
            {
                Color darker = DarkenColor(speaker.PanelColor, panelBackgroundDarkenSteps * panelBackgroundDarkenStepAmount);
                darker.a = panelFallback.a;
                panelBackground.color = darker;
            }
            else
            {
                panelBackground.color = panelFallback;
            }
        }

        if (activeNameText != null)
        {
            Color nameFallback = activeNameText == characterNameTextRight ? nameTextRightDefault : nameTextLeftDefault;
            activeNameText.color = usePanelColor && tintNameWithPanelColor ? BrightenColor(speaker.PanelColor, nameBrightenSteps * nameBrightenStepAmount) : nameFallback;
        }
    }

    private static Color DarkenColor(Color color, float amount)
    {
        Color.RGBToHSV(color, out float h, out float sat, out float v);
        return Color.HSVToRGB(h, sat, Mathf.Clamp01(v - amount));
    }

    private static Color BrightenColor(Color color, float amount)
    {
        Color.RGBToHSV(color, out float h, out float sat, out float v);

        float boosted = v + amount;
        float overflow = Mathf.Max(0f, boosted - 1f);

        Color result = Color.HSVToRGB(h, Mathf.Clamp01(sat - overflow), Mathf.Clamp01(boosted));
        result.a = 1f;
        return result;
    }

    private void ShowSpeakerPortrait(Object speaker, Sprite portrait)
    {
        bool onLeft = IsSpeakerOnLeft(speaker);

        Image active = onLeft ? characterPortrait : characterPortraitRight;
        Image other = onLeft ? characterPortraitRight : characterPortrait;

        if (active != null)
        {
            if (portrait != null)
            {
                bool wasHidden = !active.gameObject.activeSelf;
                active.sprite = portrait;
                active.color = activePortraitColor;
                active.gameObject.SetActive(true);
                if (wasHidden) SetPortraitScaleTarget(active, inactivePortraitScale, true);
                SetPortraitScaleTarget(active, activePortraitScale);
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

        SetPortraitScaleTarget(portrait, inactivePortraitScale);

        if (hideInactivePortrait) portrait.gameObject.SetActive(false);
        else portrait.color = inactivePortraitColor;
    }

    private IEnumerator TypeLine(DialogueLine line)
    {
        isTyping = true;
        activeDialogueText.text = "";

        foreach (char character in line.Text)
        {
            activeDialogueText.text += character;

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

        activeDialogueText.text =
            currentDialogue.Lines[currentLineIndex].Text;

        isTyping = false;

        UpdateContinueIndicator();
    }

    private void SetPromptVisible(bool visible, bool instant = false)
    {
        if (continuePrompt == null) return;

        if (continuePromptGroup == null)
        {
            continuePromptGroup = continuePrompt.GetComponent<CanvasGroup>();
            if (continuePromptGroup == null) continuePromptGroup = continuePrompt.AddComponent<CanvasGroup>();
        }

        if (promptFadeCoroutine != null)
        {
            StopCoroutine(promptFadeCoroutine);
            promptFadeCoroutine = null;
        }

        continuePrompt.SetActive(true);

        float target = visible ? 1f : 0f;
        float duration = visible ? promptFadeInDuration : promptFadeOutDuration;

        if (instant || duration <= 0f || !isActiveAndEnabled)
        {
            continuePromptGroup.alpha = target;
            continuePrompt.SetActive(visible);
            return;
        }

        promptFadeCoroutine = StartCoroutine(PromptFadeRoutine(target, duration));
    }

    private IEnumerator PromptFadeRoutine(float target, float duration)
    {
        float start = continuePromptGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            continuePromptGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        continuePromptGroup.alpha = target;
        if (target <= 0f) continuePrompt.SetActive(false);
        promptFadeCoroutine = null;
    }

    private void UpdateContinueIndicator()
    {
        SetPromptVisible(true);

        if (nextDialogueIndicator == null)
            return;

        bool isLastLine =
            currentLineIndex >= currentDialogue.Lines.Length - 1;

        nextDialogueIndicator.SetActive(!isLastLine || !hideContinueIndicatorOnLastLine);
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