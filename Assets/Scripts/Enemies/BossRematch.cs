using System.Collections;
using Enemies;
using FMODUnity;
using GameUI;
using UnityEngine;

public class BossRematch : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactionPrompt = "Rematch";
    [SerializeField] private bool shouldStopPlayer = false;
    [SerializeField] private bool requireGrounded = true;
    [SerializeField] private SpriteOutlineToggle outlineToggle;
    [SerializeField] private Collider2D promptCollider;
    [SerializeField] private EventReference rematchSound;
    [SerializeField] private string promptMessage = "Rematch the boss?";

    private BossTrigger owner;
    private ConfirmWindow confirmWindow;

    public string InteractionPrompt => interactionPrompt;
    public bool CanInteract => owner != null && PlayerGroundedIfRequired;
    public bool ShouldStopPlayerMovement => shouldStopPlayer;
    public SpriteOutlineToggle OutlineToggle => outlineToggle;
    public Collider2D PromptCollider => promptCollider;

    private bool PlayerGroundedIfRequired
    {
        get
        {
            if (!requireGrounded) return true;
            Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player == null || player.Controller == null || player.Controller.IsGrounded;
        }
    }

    public void SetOwner(BossTrigger trigger) => owner = trigger;

    private void Awake()
    {
        if (outlineToggle == null) outlineToggle = GetComponent<SpriteOutlineToggle>();
        if (promptCollider == null) promptCollider = GetComponent<Collider2D>();
    }

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        if (confirmWindow == null)
            confirmWindow = FindFirstObjectByType<ConfirmWindow>(FindObjectsInactive.Include);

        if (confirmWindow == null)
        {
            Debug.LogWarning("[BossRematch] No ConfirmWindow found");
            StartRematch();
            return;
        }
        
        confirmWindow.Show(promptMessage, OnRematchConfirmed);
    }

    private void OnRematchConfirmed() => StartCoroutine(StartRematchWhenReleased());

    private IEnumerator StartRematchWhenReleased()
    {
        float waited = 0f;

        while (UIManager.Instance != null && UIManager.Instance.IsInputLocked && waited < 2f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        StartRematch();
    }

    private void StartRematch()
    {
        if (owner == null)
            return;
        
        if(owner.RequestRematch() && !rematchSound.IsNull)
            AudioManager.PlaySFX(rematchSound, transform.position);
    }
}
