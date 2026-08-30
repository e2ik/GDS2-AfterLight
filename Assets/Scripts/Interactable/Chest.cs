using UnityEngine;

public class Chest : MonoBehaviour, IInteractable
{
    private bool isOpened = false;

    private string chestID;
    public string ChestID => chestID;

    [Header("Loot Configuration")]
    [SerializeField] private InventoryItemBase lootItem;
    [SerializeField] private WorldItem worldItemPrefab;

    [Header("Pop Physics Settings")]
    [SerializeField] private float popForce = 5f;
    [SerializeField] private float minHorizontalAngle = -0.4f;
    [SerializeField] private float maxHorizontalAngle = 0.4f;

    public string InteractionPrompt => "Open Chest";
    public bool CanInteract => !isOpened;
    public bool ShouldStopPlayerMovement => false;

    private void Awake()
    {
        chestID = GetHierarchyPath(transform);
    }

    private string GetHierarchyPath(Transform current)
    {
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path + $"[{current.GetSiblingIndex()}]";
        }
        return $"{gameObject.scene.name}:{path}[{transform.GetSiblingIndex()}]";
    }

    private void Start()
    {
        if (SaveManager.Instance == null) return;

        isOpened = SaveManager.Instance.IsChestOpened(chestID);
        if (isOpened)
        {
            ApplyOpenedVisualState();
        }
    }

    public void Interact(Player player)
    {
        if (lootItem == null)
        {
            Debug.LogWarning($"[Chest] Chest '{chestID}' opened, but no loot item is assigned!");
            CompleteOpening();
            return;
        }

        SpawnAndPopLoot();
        CompleteOpening();
    }

    private void SpawnAndPopLoot()
    {
        if (worldItemPrefab == null)
        {
            Debug.LogError($"[Chest] WorldItemPrefab is not assigned on Chest '{chestID}'!");
            return;
        }

        Vector3 spawnPosition = transform.position + new Vector3(0f, 0.5f, 0f);

        WorldItem droppedItem = Instantiate(worldItemPrefab, spawnPosition, Quaternion.identity);
        droppedItem.Initialize(lootItem);

        float randomX = Random.Range(minHorizontalAngle, maxHorizontalAngle);
        Vector2 popDirection = new Vector2(randomX, 1.0f).normalized;

        droppedItem.PopOut(popDirection, popForce);
    }

    private void CompleteOpening()
    {
        isOpened = true;
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.MarkChestOpened(chestID);
        }
        ApplyOpenedVisualState();
    }

    private void ApplyOpenedVisualState()
    {
        // Swap sprite or trigger open chest animation here
    }
}