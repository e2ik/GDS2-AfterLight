using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class EquipmentSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Sprite emptySlotSprite;

    [Header("Text Settings")]
    [SerializeField] private bool showDefaultTextWhenEmpty = true;

    [Header("Equipped Scale Settings")]
    [SerializeField] private float equippedIconScale = 1.2f;

    [Header("Icon Colour Settings")]
    [SerializeField] private Color emptySlotColor = Color.white;
    [SerializeField] private Color equippedItemColor = Color.white;

    [Header("Border Colour Settings")]
    [SerializeField] private Image borderImage;
    [SerializeField] private bool colorBorderByRarity = true;
    [SerializeField] private Color noRarityBorderColor = Color.white;

    [Header("Interaction")]
    [SerializeField] private Button actionButton;
    [SerializeField] private Graphic selectionGraphic;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool hideWhenUnselected = true;
    [SerializeField] private bool pulseWhenSelected = true;
    [SerializeField, Min(1f)] private float pulseScale = 1.2f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 1.5f;
    [SerializeField, Min(0f)] private float controllerTooltipDelay = 0.5f;

    [Header("Selected Slot Pulse")]
    [SerializeField] private bool pulseSlotWhenSelected = true;
    [SerializeField, Min(1f)] private float slotPulseScale = 1.05f;
    [SerializeField, Min(0.01f)] private float slotPulseSpeed = 1.5f;

    [Header("Selected Border")]
    [SerializeField] private bool colorBorderWhenSelected = true;
    [SerializeField] private Color selectedBorderColor = Color.yellow;
    [SerializeField] private bool pulseSelectedBorder = true;
    [SerializeField, Range(0f, 1f)] private float selectedBorderMinBlend = 0.5f;
    [SerializeField, Min(0.01f)] private float selectedBorderPulseSpeed = 2f;

    private object currentItem;
    public object CurrentItem => currentItem;
    public Selectable Selectable => actionButton;

    private InventoryDisplay cachedDisplay;
    private UIWindowAnimator windowAnimator;
    private RectTransform rectTransform;
    private bool isSelected;
    private bool isPointerOver;
    private bool tooltipWanted;
    private Vector3 selectionBaseScale = Vector3.one;
    private Vector3 slotBaseScale = Vector3.one;
    private float slotPulseTime;
    private float pulseTime;

    private Vector3 originalIconScale = Vector3.one;
    private string defaultSlotName;
    private Color emptyBorderColor = Color.white;
    private Color restBorderColor = Color.white;
    private Color highlightBorderColor = Color.white;
    private float highlightWeight;

    private void Awake()
    {
        if (borderImage == null) borderImage = GetComponent<Image>();
        if (borderImage != null) emptyBorderColor = restBorderColor = borderImage.color;

        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
            originalIconScale = iconImage.transform.localScale;
        }

        if (nameText != null)
        {
            nameText.raycastTarget = false;
            defaultSlotName = nameText.text; 
        }

        rectTransform = (RectTransform)transform;
        slotBaseScale = transform.localScale;
        windowAnimator = GetComponentInParent<UIWindowAnimator>(true);

        if (actionButton == null) actionButton = GetComponent<Button>();
        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(OnSlotClicked);
            actionButton.onClick.AddListener(OnSlotClicked);
        }

        if (selectionGraphic != null)
        {
            selectionGraphic.raycastTarget = false;
            selectionBaseScale = selectionGraphic.rectTransform.localScale;
        }
        ApplySelectionVisual();
    }

    private InventoryDisplay Display
    {
        get
        {
            if (cachedDisplay == null) cachedDisplay = GetComponentInParent<InventoryDisplay>(true);
            return cachedDisplay;
        }
    }

    private void Update()
    {
        GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool selected = current != null && current.transform.IsChildOf(transform);

        if (selected != isSelected)
        {
            isSelected = selected;
            ApplySelectionVisual();
            ApplyBorderColor();
            if (isSelected) Display?.OnSlotFocused(currentItem);
        }

        UpdateSelectionPulse();
        UpdateSlotPulse();
        if (isSelected && colorBorderWhenSelected && pulseSelectedBorder) ApplyBorderColor();
        UpdateTooltipState();
    }

    private void OnDisable()
    {
        if (tooltipWanted && ItemTooltip.Instance != null) ItemTooltip.Instance.HideTooltip(rectTransform);

        isSelected = false;
        isPointerOver = false;
        tooltipWanted = false;
        slotPulseTime = 0f;
        transform.localScale = slotBaseScale;
        ApplySelectionVisual();
        ApplyBorderColor();
    }

    private void OnDestroy()
    {
        if (actionButton != null) actionButton.onClick.RemoveListener(OnSlotClicked);
    }

    public void SetItem(object item)
    {
        currentItem = item;

        if (!tooltipWanted) return;

        if (currentItem == null)
        {
            tooltipWanted = false;
            if (ItemTooltip.Instance != null) ItemTooltip.Instance.HideTooltip(rectTransform);
        }
        else
        {
            TriggerTooltip();
        }
    }

    private void OnSlotClicked()
    {
        InventoryDisplay display = Display;
        if (display == null || !display.IsOpen || currentItem == null) return;

        PlayerEquipmentManager equip = ItemActionFactory.GetEquipment();
        if (!ItemActionFactory.CanUnequip(currentItem, equip)) return;

        ItemActionFactory.ToggleEquip(currentItem, equip);
        display.RefreshUI();
    }

    private void ApplySelectionVisual()
    {
        if (selectionGraphic == null) return;
        selectionGraphic.color = isSelected ? selectedColor : unselectedColor;
        selectionGraphic.enabled = isSelected || !hideWhenUnselected;

        pulseTime = 0f;
        selectionGraphic.rectTransform.localScale = selectionBaseScale;
    }

    private void UpdateSlotPulse()
    {
        if (!pulseSlotWhenSelected || !isSelected)
        {
            if (slotPulseTime != 0f)
            {
                slotPulseTime = 0f;
                transform.localScale = slotBaseScale;
            }
            return;
        }

        slotPulseTime += Time.unscaledDeltaTime * slotPulseSpeed;
        float t = (1f - Mathf.Cos(slotPulseTime * Mathf.PI * 2f)) * 0.5f;
        transform.localScale = slotBaseScale * Mathf.Lerp(1f, slotPulseScale, t);
    }

    private void UpdateSelectionPulse()
    {
        if (selectionGraphic == null || !pulseWhenSelected || !isSelected) return;

        pulseTime += Time.unscaledDeltaTime * pulseSpeed;
        float t = (1f - Mathf.Cos(pulseTime * Mathf.PI * 2f)) * 0.5f;
        selectionGraphic.rectTransform.localScale = selectionBaseScale * Mathf.Lerp(1f, pulseScale, t);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        SelectOnHover();
        Display?.OnSlotFocused(currentItem);
        UpdateTooltipState();
    }

    private void SelectOnHover()
    {
        if (EventSystem.current == null || actionButton == null) return;
        if (!actionButton.IsActive() || !actionButton.IsInteractable()) return;
        if (EventSystem.current.currentSelectedGameObject == actionButton.gameObject) return;

        EventSystem.current.SetSelectedGameObject(actionButton.gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        UpdateTooltipState();
    }

    private void UpdateTooltipState()
    {
        bool windowReady = windowAnimator == null
            || (!windowAnimator.IsAnimating && windowAnimator.SecondsSinceSettled >= controllerTooltipDelay);

        bool wanted = currentItem != null
            && (InputModeTracker.IsUsingMouse ? isPointerOver : (isSelected && windowReady));

        if (Display != null && Display.IsLorePanelOpen) wanted = false;
        if (wanted == tooltipWanted) return;

        tooltipWanted = wanted;

        if (wanted) TriggerTooltip();
        else if (ItemTooltip.Instance != null) ItemTooltip.Instance.HideTooltip(rectTransform);
    }

    private void TriggerTooltip()
    {
        if (ItemTooltip.Instance == null) return;

        InventoryItemBase def = ItemActionFactory.GetDefinition(currentItem);
        if (def == null)
        {
            ItemTooltip.Instance.HideTooltip(rectTransform);
            return;
        }

        InventoryDisplay display = Display;
        RectTransform dock = display != null ? display.TooltipDock : null;
        TooltipAnchorSettings settings = display != null ? display.TooltipAnchor : null;
        TooltipActions actions = ItemActionFactory.ForEquipped(currentItem);
        string body = ItemActionFactory.BuildTooltipBody(currentItem);

        ItemTooltip.Instance.ShowTooltipAnchored(def.UIName, body, rectTransform, settings, dock, actions);
    }

    public void DisplayItem(Sprite sprite, string itemName, ERarity? rarity = null)
    {
        bool hasItem = sprite != null;

        if (iconImage != null)
        {
            Sprite activeSprite = hasItem ? sprite : emptySlotSprite;

            iconImage.sprite = activeSprite;
            iconImage.enabled = (activeSprite != null);

            if (activeSprite != null)
            {
                iconImage.color = hasItem ? equippedItemColor : emptySlotColor;
            }
            iconImage.transform.localScale = hasItem ? originalIconScale * equippedIconScale : originalIconScale;
        }

        if (nameText != null)
        {
            if (hasItem)
            {
                nameText.gameObject.SetActive(false);
            }
            else
            {
                nameText.gameObject.SetActive(showDefaultTextWhenEmpty);
                nameText.text = defaultSlotName;
            }
        }

        UpdateBorderColor(hasItem, rarity);
    }

    private void UpdateBorderColor(bool hasItem, ERarity? rarity)
    {
        if (borderImage == null) return;

        if (!hasItem || !colorBorderByRarity)
            restBorderColor = emptyBorderColor;
        else
            restBorderColor = rarity.HasValue && GameManager.Instance != null
                ? GameManager.Instance.GetRarityColor(rarity.Value)
                : noRarityBorderColor;

        ApplyBorderColor();
    }

    public void ApplyHighlight(Color color, float weight)
    {
        highlightBorderColor = color;
        highlightWeight = Mathf.Clamp01(weight);
        ApplyBorderColor();
    }

    private void ApplyBorderColor()
    {
        if (borderImage == null) return;

        Color color = Color.Lerp(restBorderColor, highlightBorderColor, highlightWeight);

        if (isSelected && colorBorderWhenSelected)
        {
            float blend = 1f;
            if (pulseSelectedBorder)
            {
                float wave = (1f - Mathf.Cos(Time.unscaledTime * selectedBorderPulseSpeed * Mathf.PI * 2f)) * 0.5f;
                blend = Mathf.Lerp(selectedBorderMinBlend, 1f, wave);
            }
            color = Color.Lerp(color, selectedBorderColor, blend);
        }

        borderImage.color = color;
    }

    public void ClearSlot()
    {
        DisplayItem(null, null);
        SetItem(null);
    }
}