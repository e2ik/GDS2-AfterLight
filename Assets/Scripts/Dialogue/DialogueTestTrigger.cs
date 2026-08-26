using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueTestTrigger : MonoBehaviour
{
    [SerializeField] private DialogueData testDialogue;
    [SerializeField] private Player player;

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (DialogueManager.Instance == null)
            {
                Debug.LogWarning("[DialogueTestTrigger] DialogueManager not found.");
                return;
            }

            DialogueManager.Instance.StartDialogue(testDialogue, player);
        }
    }
}