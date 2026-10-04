using UnityEngine;
using System.Collections;
using FMODUnity;

public class TransitionDoor : MonoBehaviour, IInteractable
{
    public enum DoorMode
    {
        Normal,
        RequireKey,
        Broken
    }

    [SerializeField] private string interactionPrompt = "Enter";
    [SerializeField] private bool canInteract = true;
    [SerializeField] private bool shouldStopPlayer = true;
    [SerializeField] private SpriteOutlineToggle outlineToggle;
    [SerializeField] private Collider2D promptCollider;
    public string InteractionPrompt => interactionPrompt;
    public bool CanInteract => canInteract && currentTransition == null && !isDoorAnimating;
    public bool ShouldStopPlayerMovement => shouldStopPlayer;
    public SpriteOutlineToggle OutlineToggle => outlineToggle;
    public Collider2D PromptCollider => promptCollider;

    [SerializeField] private SceneAreaState sceneAreaState;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Access")]
    [SerializeField] private DoorMode doorMode = DoorMode.Normal;
    [SerializeField] private KeyDefinition requiredKey;
    [SerializeField] private string missingKeyMessage = "I need the [{0}]...";
    [SerializeField] private Color keyNameColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField, Min(0f)] private float missingKeyMessageDuration = 2f;
    [SerializeField] private DialogueEffect missingKeyMessageEffect = DialogueEffect.Default;
    [SerializeField] private string brokenMessage = "It won't budge...";
    [SerializeField, Min(0f)] private float brokenMessageDuration = 2f;
    [SerializeField] private DialogueEffect brokenMessageEffect = DialogueEffect.Default;
    [SerializeField] private EventReference lockedEvent;

    [Header("Door Animation")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string openTriggerName = "Open";
    [SerializeField] private string doorIdleState = "Idle";
    [SerializeField] private float maxAnimationWait = 5f;

    [Header("Door SFX")]
    [SerializeField] private EventReference doorOpenEvent;

    private Coroutine currentTransition;
    private bool isDoorAnimating;

    private void Awake()
    {
        if (outlineToggle == null) outlineToggle = GetComponent<SpriteOutlineToggle>();
        if (promptCollider == null) promptCollider = GetComponent<Collider2D>();
    }

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        if (doorMode == DoorMode.Broken)
        {
            ShowMessage(player, brokenMessage, brokenMessageDuration, brokenMessageEffect);
            return;
        }

        if (doorMode == DoorMode.RequireKey && !PlayerHasRequiredKey(player))
        {
            string message = missingKeyMessage;
            if (requiredKey != null)
            {
                string coloredName = $"<color=#{ColorUtility.ToHtmlStringRGB(keyNameColor)}>{requiredKey.UIName}</color>";
                message = string.Format(missingKeyMessage, coloredName);
            }

            ShowMessage(player, message, missingKeyMessageDuration, missingKeyMessageEffect);
            return;
        }

        currentTransition = StartCoroutine(TransitionRoutine(player));
    }

    private void ShowMessage(Player player, string message, float duration, DialogueEffect effect)
    {
        AudioManager.PlaySFX(lockedEvent, transform.position);

        if (player != null && player.Controller != null)
            player.Controller.FreezeMovement(false);

        if (player != null)
            Tutorial.TutorialSpeechBubblePool.Instance?.Show(message, player.transform, duration, effect);
    }

    private bool PlayerHasRequiredKey(Player player)
    {
        if (requiredKey == null)
        {
            Debug.LogWarning($"{name}: Door Mode is Require Key but no Required Key is assigned, allowing entry.", this);
            return true;
        }

        PlayerInventorySO inv = player != null && player.Inventory != null ? player.Inventory.currentInventory : null;
        if (inv == null) return false;

        return inv.KeyInstances != null && inv.KeyInstances.Exists(k => k.InstItemID == requiredKey.ItemID);
    }

    private IEnumerator TransitionRoutine(Player player)
    {
        if (doorAnimator != null)
        {
            isDoorAnimating = true;
            doorAnimator.SetTrigger(openTriggerName);
            AudioManager.PlaySFX(doorOpenEvent, transform.position);
        }

        if (player.Controller != null)
        {
            player.Controller.InputEnabled = false;
        }

        yield return FadeOut();

        if (sceneAreaState != null)
        {
            AreaSide newSide = sceneAreaState.CurrentSide == AreaSide.Interior ? AreaSide.Exterior : AreaSide.Interior;

            sceneAreaState.SetSide(newSide);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetAreaSide(newSide);
                GameManager.Instance.ApplyAreaSide(newSide);
            }
        }

        yield return FadeIn();

        if (player.Controller != null)
        {
            player.Controller.InputEnabled = true;
            player.Controller.FreezeMovement(false);
        }

        currentTransition = null;

        if (doorAnimator != null) yield return WaitForDoorAnimation();
    }

    private IEnumerator WaitForDoorAnimation()
    {
        isDoorAnimating = true;
        float elapsed = 0f;
        int idleHash = Animator.StringToHash(doorIdleState);

        yield return null;

        while (elapsed < maxAnimationWait)
        {
            bool inTransition = doorAnimator.IsInTransition(0);
            AnimatorStateInfo state = doorAnimator.GetCurrentAnimatorStateInfo(0);

            bool finished = string.IsNullOrEmpty(doorIdleState)
                ? !inTransition && (state.loop || state.normalizedTime >= 1f)
                : !inTransition && state.shortNameHash == idleHash;

            if (finished) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        doorAnimator.ResetTrigger(openTriggerName);
        isDoorAnimating = false;
    }

    private IEnumerator FadeOut()
    {
        if (FadeCanvasController.Instance == null)
        {
            Debug.LogError($"[TransitionDoor] No FadeCanvasController found — is the master scene loaded?", this);
            yield break;
        }

        yield return FadeCanvasController.Instance.FadeOut(fadeDuration);
    }

    private IEnumerator FadeIn()
    {
        if (FadeCanvasController.Instance == null)
        {
            Debug.LogError($"[TransitionDoor] No FadeCanvasController found — is the master scene loaded?", this);
            yield break;
        }

        yield return FadeCanvasController.Instance.FadeIn(fadeDuration);
    }
}