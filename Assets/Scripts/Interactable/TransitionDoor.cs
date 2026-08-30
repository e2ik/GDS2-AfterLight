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
    [SerializeField] private CanvasGroup fadeCanvas;
    [SerializeField] private float fadeDuration = 0.5f;

    private Coroutine currentTransition;

    public void Interact(Player player)
    {
        if (!canInteract || currentTransition != null) return;
        currentTransition = StartCoroutine(TransitionRoutine(player));
    }

    private IEnumerator TransitionRoutine(Player player)
    {
        canInteract = false;
        
        if (player.Controller != null)
        {
            player.Controller.InputEnabled = false;
        }

        yield return Fade(0f, 1f);

        if (sceneAreaState != null)
        {
            AreaSide newSide = sceneAreaState.CurrentSide == AreaSide.Interior ? AreaSide.Exterior : AreaSide.Interior;
            
            sceneAreaState.SetSide(newSide);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetAreaSide(newSide);
                GameManager.Instance.ApplyAreaSide(newSide);
            }

            if (SaveManager.Instance?.GetSaveData()?.progress != null)
            {
                SaveManager.Instance.GetSaveData().progress.lastAreaSide = newSide;
                SaveManager.Instance.CommitToDisk();
            }
        }

        yield return Fade(1f, 0f);

        if (player.Controller != null)
        {
            player.Controller.InputEnabled = true;
            player.Controller.FreezeMovement(false);
        }
        
        canInteract = true;
        currentTransition = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeCanvas == null)
        {
            Debug.LogError($"[TransitionDoor] Fade Canvas is missing on {gameObject.name}!");
            yield break;
        }

        float t = 0f;
        fadeCanvas.alpha = from;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeCanvas.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        
        fadeCanvas.alpha = to;
    }
}