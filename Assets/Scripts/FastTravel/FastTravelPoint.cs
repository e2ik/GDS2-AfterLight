using System.Collections;
using UnityEngine;

public class FastTravelPoint : MonoBehaviour, IInteractable
{
    [Header("Fast Travel Config")]
    [SerializeField] private FastTravelNodeSO nodeData;
    [SerializeField] private WorldMapStateSO worldMapState;

    [Header("Interaction Settings")]
    [SerializeField] private bool canBeInteractedWith = true;
    [SerializeField] private float mapOpenDelay = 0.5f;

    private static readonly int IsDiscoveredHash = Animator.StringToHash("isDiscovered");
    private static readonly int IsIdleHash = Animator.StringToHash("isIdle");
    private static readonly int IsInteractedHash = Animator.StringToHash("isInteracted");

    private Animator anim;
    private bool isInteracting = false;

    private void Awake()
    {
        anim = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (worldMapState != null)
        {
            worldMapState.OnNodeUnlocked += OnNodeUnlocked;
            worldMapState.OnStateLoaded += UpdateVisualState;
            worldMapState.OnStateReset += UpdateVisualState;
        }
    }

    private void OnDisable()
    {
        if (worldMapState != null)
        {
            worldMapState.OnNodeUnlocked -= OnNodeUnlocked;
            worldMapState.OnStateLoaded -= UpdateVisualState;
            worldMapState.OnStateReset -= UpdateVisualState;
        }
    }

    private void Start()
    {
        UpdateVisualState();
    }

    private void OnNodeUnlocked(FastTravelNodeSO unlockedNode)
    {
        if (unlockedNode == nodeData && !isInteracting)
        {
            UpdateVisualState();
        }
    }

    public bool CanInteract
    {
        get
        {
            if (MapUIManager.Instance != null && MapUIManager.Instance.IsMapOpen)
                return false;

            return canBeInteractedWith && !isInteracting;
        }
    }

    public string InteractionPrompt => (worldMapState != null && worldMapState.IsUnlocked(nodeData))
        ? $"Travel from {nodeData.displayName}" 
        : $"Unlock {nodeData.displayName}";

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        StartCoroutine(InteractionRoutine());
    }

    private IEnumerator InteractionRoutine()
    {
        isInteracting = true;

        if (anim != null)
        {
            anim.SetTrigger(IsInteractedHash);
        }

        bool isAlreadyUnlocked = worldMapState != null && worldMapState.IsUnlocked(nodeData);

        if (!isAlreadyUnlocked && worldMapState != null)
        {
            worldMapState.UnlockNode(nodeData);
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveProgressAtLocation(nodeData.targetSceneName, nodeData.spawnAnchorID);
        }

        if (!isAlreadyUnlocked && mapOpenDelay > 0f)
        {
            yield return new WaitForSeconds(mapOpenDelay);
        }

        UpdateVisualState();

        if (MapUIManager.Instance != null)
        {
            MapUIManager.Instance.OpenMap(nodeData);
        }

        isInteracting = false;
    }

    public void UpdateVisualState()
    {
        if (anim == null || worldMapState == null || nodeData == null) return;

        bool isUnlocked = worldMapState.IsUnlocked(nodeData);

        anim.SetBool(IsDiscoveredHash, isUnlocked);
        anim.SetBool(IsIdleHash, !isUnlocked);
    }
}