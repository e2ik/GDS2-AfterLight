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

    private int interactionDisableCount;
    private int lastUnblockFrame = -1;
    public bool InteractionEnabled => interactionDisableCount <= 0;

    private IInteractable currentInteractable;
    private Transform currentInteractableTransform;
    private SpriteOutlineToggle currentOutlineToggle;

    public event System.Action<Transform, Collider2D> OnInteractionTargetChanged;

    private Player player;
    private PlayerController playerController;
    private InputAction interactAction;
    private Transform ownRoot;

    private void Awake()
    {
        player = GetComponent<Player>();
        playerController = GetComponent<PlayerController>();
        ownRoot = transform.root;

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

        if (currentInteractable == null || !interactAction.WasPressedThisFrame()) return;
        if (!CanPlayerInteract()) return;
        if (!currentInteractable.CanInteract) return;

        if (currentInteractable.ShouldStopPlayerMovement)
        {
            playerController.FreezeMovement(true);
        }

        currentInteractable.Interact(player);
    }

    private bool CanPlayerInteract()
    {
        return playerController.InputEnabled
               && !playerController.IsUILocked
               && !playerController.IsPhysicsSuspended
               && Time.frameCount != lastUnblockFrame;
    }

    public void SetInteractionBlocked(bool blocked)
    {
        if (blocked)
        {
            interactionDisableCount++;
            return;
        }

        interactionDisableCount = Mathf.Max(0, interactionDisableCount - 1);
        if (interactionDisableCount == 0) lastUnblockFrame = Time.frameCount;
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
            if (hit.collider.transform.root == ownRoot) continue;

            IInteractable candidate = hit.collider.GetComponent<IInteractable>();
            if (candidate == null || !candidate.CanInteract) continue;

            hitInteractable = candidate;
            hitTransform = hit.collider.transform;
            break;
        }

        if (hitInteractable != currentInteractable)
        {
            if (currentOutlineToggle != null)
            {
                currentOutlineToggle.EndHighlight();
                currentOutlineToggle = null;
            }

            currentInteractable = hitInteractable;
            currentInteractableTransform = hitTransform;

            if (currentInteractable != null)
            {
                currentOutlineToggle = currentInteractable.OutlineToggle;
                if (currentOutlineToggle != null)
                {
                    currentOutlineToggle.BeginHighlight();
                }
            }

            OnInteractionTargetChanged?.Invoke(currentInteractableTransform, currentInteractable?.PromptCollider);
        }
    }

    private void ClearCurrentInteractable()
    {
        if (currentOutlineToggle != null)
        {
            currentOutlineToggle.EndHighlight();
            currentOutlineToggle = null;
        }

        currentInteractable = null;
        currentInteractableTransform = null;

        OnInteractionTargetChanged?.Invoke(null, null);
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