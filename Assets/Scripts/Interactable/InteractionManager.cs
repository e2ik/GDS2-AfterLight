using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerController))]
public class InteractionManager : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float interactionRange = 1.5f;
    [SerializeField] private LayerMask interactableLayers = ~0;
    [SerializeField] private Vector2 raycastOriginOffset = Vector2.zero;

    public bool InteractionEnabled { get; set; } = true;

    private IInteractable currentInteractable;
    private Transform currentInteractableTransform;
    private SpriteOutlineToggle currentOutlineToggle;

    public event System.Action<string> OnInteractionPromptChanged;
    public event System.Action<Transform> OnInteractionTargetChanged;

    private Player player;
    private PlayerController playerController;
    private InputAction interactAction;

    private void Awake()
    {
        player = GetComponent<Player>();
        playerController = GetComponent<PlayerController>();

        PlayerInput playerInput = GetComponent<PlayerInput>();
        interactAction = playerInput.actions["Interact"];

    }

    private void Update()
    {
        if (!InteractionEnabled)
        {
            if (currentInteractable != null)
                ClearCurrentInteractable();

            return;
        }

        DetectInteractable();

        if (currentInteractable != null && interactAction.WasPressedThisFrame())
        {
            if (currentInteractable.ShouldStopPlayerMovement)
            {
                // Freeze movement using your built-in counter system
                playerController.FreezeMovement(true);
            }

            currentInteractable.Interact(player);
        }
    }

    private void DetectInteractable()
    {
        IInteractable hitInteractable = null;
        Transform hitTransform = null;

        Vector2 origin = (Vector2)transform.position + raycastOriginOffset;
        Vector2 direction = playerController.FacingDirection == 1 ? Vector2.right : Vector2.left;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, interactionRange, interactableLayers);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.transform.root == transform.root) continue;

            IInteractable candidate = hit.collider.GetComponent<IInteractable>();
            if (candidate == null || !candidate.CanInteract) continue;

            hitInteractable = candidate;
            hitTransform = hit.collider.transform;
            break;
        }

        if (hitInteractable != currentInteractable)
        {
            // 1. Turn off outline on previous object (if any)
            if (currentOutlineToggle != null)
            {
                currentOutlineToggle.SetOutline(false);
                currentOutlineToggle = null;
            }

            currentInteractable = hitInteractable;
            currentInteractableTransform = hitTransform;

            // 2. Fetch and turn on outline on newly focused object
            if (currentInteractableTransform != null)
            {
                currentOutlineToggle = currentInteractableTransform.GetComponent<SpriteOutlineToggle>();
                if (currentOutlineToggle != null)
                {
                    currentOutlineToggle.SetOutline(true);
                }
            }

            OnInteractionTargetChanged?.Invoke(currentInteractableTransform);

            OnInteractionPromptChanged?.Invoke(currentInteractable?.InteractionPrompt);
        }
    }

    private void ClearCurrentInteractable()
    {
        if (currentOutlineToggle != null)
        {
            currentOutlineToggle.SetOutline(false);
            currentOutlineToggle = null;
        }

        currentInteractable = null;
        currentInteractableTransform = null;

        OnInteractionTargetChanged?.Invoke(null);

        OnInteractionPromptChanged?.Invoke(null);
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 origin = (Vector2)transform.position + raycastOriginOffset;
        int facing = Application.isPlaying && playerController != null ? playerController.FacingDirection : 1;
        Vector2 direction = facing == 1 ? Vector2.right : Vector2.left;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + direction * interactionRange);
    }
}