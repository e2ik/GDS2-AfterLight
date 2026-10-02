using UnityEngine;
using System.Collections;
using FMODUnity;

public class TransitionDoor : MonoBehaviour, IInteractable
{
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
        currentTransition = StartCoroutine(TransitionRoutine(player));
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