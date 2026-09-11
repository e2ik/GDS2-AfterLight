using System.Collections.Generic;
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

    private readonly struct DisplayItem
    {
        public readonly object Item;
        public readonly int PickupOrder;

        public DisplayItem(object item, int pickupOrder)
        {
            Item = item;
            PickupOrder = pickupOrder;
        }
    }

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

    private static void AddItems<T>(List<T> items, System.Func<T, int> pickupOrderSelector, List<DisplayItem> into) where T : class
    {
        if (items == null) return;

        foreach (T item in items)
        {
            if (item != null) into.Add(new DisplayItem(item, pickupOrderSelector(item)));
        }
    }

    private static void ApplyToSlot(InventorySlot slot, object item)
    {
        switch (item)
        {
            case SecondaryGemInstance gem: slot.SetupSlot(gem); break;
            case GearInstance gear: slot.SetupSlot(gear); break;
            case PrimaryGemInstance primaryGem: slot.SetupSlot(primaryGem); break;
            case WeaponInstance weapon: slot.SetupSlot(weapon); break;
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

        List<DisplayItem> displayItems = new List<DisplayItem>();

        AddItems(activeInventory.SecondaryGems, g => g.PickupOrder, displayItems);
        AddItems(activeInventory.GearInstances, g => g.PickupOrder, displayItems);
        AddItems(activeInventory.PrimaryGems, g => g.PickupOrder, displayItems);
        AddItems(activeInventory.Weapons, w => w.PickupOrder, displayItems);

        displayItems.Sort((a, b) => a.PickupOrder.CompareTo(b.PickupOrder));

        foreach (DisplayItem entry in displayItems)
        {
            GameObject newSlot = Instantiate(slotPrefab, slotContainer);
            if (newSlot.TryGetComponent(out InventorySlot slotScript))
            {
                ApplyToSlot(slotScript, entry.Item);
            }
        }

        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(slotContainer as RectTransform);
    }
}