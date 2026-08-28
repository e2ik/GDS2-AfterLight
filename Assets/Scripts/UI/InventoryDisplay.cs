using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class InventoryDisplay : MonoBehaviour
{
    [Header("UI Container")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("Animation Reference")]
    [SerializeField] private UIWindowAnimator windowAnimator;

    private CanvasGroup canvasGroup;
    private PlayerInventoryManager invManager;
    private bool isVisible = false;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (windowAnimator == null)
            windowAnimator = GetComponent<UIWindowAnimator>();
    }

    private void Start()
    {
        if (windowAnimator != null)
        {
            windowAnimator.InstantHide();
            isVisible = false;
        }
        else
        {
            SetVisibility(false);
        }
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

            if (isVisible)
            {
                RefreshUI();
            }
        }
    }

    public void ToggleInventory()
    {
        SetVisibility(!isVisible);
    }

    public void SetVisibility(bool visible)
    {
        isVisible = visible;

        if (windowAnimator != null)
        {
            if (visible)
            {
                RefreshUI();
                windowAnimator.Show();
            }
            else
            {
                windowAnimator.Hide();
            }
        }
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            if (visible)
            {
                RefreshUI();
            }
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