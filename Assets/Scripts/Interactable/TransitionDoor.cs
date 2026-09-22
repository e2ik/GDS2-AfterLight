using UnityEngine;
using System.Collections;

public class TransitionDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionPrompt = "Enter";
    [SerializeField] private bool canInteract = true;
    [SerializeField] private bool shouldStopPlayer = true;
    public string InteractionPrompt => interactionPrompt;
    public bool CanInteract => canInteract;
    public bool ShouldStopPlayerMovement => shouldStopPlayer;

    [SerializeField] private SceneAreaState sceneAreaState;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Door Animation")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string openTriggerName = "Open";

    private Coroutine currentTransition;

    public void Interact(Player player)
    {
        if (!canInteract || currentTransition != null) return;
        currentTransition = StartCoroutine(TransitionRoutine(player));
    }

    private IEnumerator TransitionRoutine(Player player)
    {
        canInteract = false;

        if (doorAnimator != null)
        {
            doorAnimator.SetTrigger(openTriggerName);
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

        canInteract = true;
        currentTransition = null;
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