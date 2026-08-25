using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class InventoryDisplay : MonoBehaviour
{
    [Header("UI Container")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;

    private CanvasGroup canvasGroup;
    private PlayerInventoryManager invManager;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnDestroy()
    {
        if (invManager != null)
        {
            invManager.OnInventoryChanged -= RefreshUI;
        }
    }

    public void RegisterInventoryManager(PlayerInventoryManager manager)
    {
        if (invManager != null)
        {
            invManager.OnInventoryChanged -= RefreshUI;
        }

        invManager = manager;

        if (invManager != null)
        {
            invManager.OnInventoryChanged += RefreshUI;
            
            PlayerEquipmentManager equipManager = invManager.GetComponent<PlayerEquipmentManager>();
            if (equipManager != null)
            {
                equipManager.OnEquipmentChanged -= RefreshUI;
                equipManager.OnEquipmentChanged += RefreshUI;
            }

            RefreshUI();
        }
    }

    public void ToggleInventory()
    {
        bool isVisible = canvasGroup.alpha > 0f;
        SetVisibility(!isVisible);
    }

    public void SetVisibility(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;

        if (visible)
        {
            RefreshUI();
        }
    }

    public void RefreshUI()
    {
        if (slotContainer == null || slotPrefab == null || invManager == null || invManager.currentInventory == null) 
            return;

        PlayerInventorySO activeInventory = invManager.currentInventory;

        for (int i = slotContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = slotContainer.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        if (activeInventory.SecondaryGems != null)
        {
            foreach (SecondaryGemInstance gem in activeInventory.SecondaryGems)
            {
                if (gem == null) continue;
                GameObject newSlot = Instantiate(slotPrefab, slotContainer);
                if (newSlot.TryGetComponent(out InventorySlot slotScript))
                {
                    slotScript.SetupSlot(gem);
                }
            }
        }

        if (activeInventory.GearInstances != null)
        {
            foreach (GearInstance gear in activeInventory.GearInstances)
            {
                if (gear == null) continue;
                GameObject newSlot = Instantiate(slotPrefab, slotContainer);
                if (newSlot.TryGetComponent(out InventorySlot slotScript))
                {
                    slotScript.SetupSlot(gear);
                }
            }
        }
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer as RectTransform);
    }
}