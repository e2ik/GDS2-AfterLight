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

    public void Interact(Player player)
    {
        StartCoroutine(TransitionRoutine(player));
    }

    private IEnumerator TransitionRoutine(Player player)
    {
        canInteract = false;
        player.Controller.InputEnabled = false;

        yield return Fade(0f, 1f);

        AreaSide newSide = sceneAreaState.CurrentSide == AreaSide.Interior ? AreaSide.Exterior : AreaSide.Interior;
        sceneAreaState.SetSide(newSide);

        yield return Fade(1f, 0f);

        player.Controller.InputEnabled = true;
        player.Controller.FreezeMovement(false);
        canInteract = true;
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        fadeCanvas.alpha = from;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeCanvas.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        fadeCanvas.alpha = to;
    }
}