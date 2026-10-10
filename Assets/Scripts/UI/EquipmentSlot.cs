using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EquipmentSlot : MonoBehaviour
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
        borderImage.color = Color.Lerp(restBorderColor, highlightBorderColor, highlightWeight);
    }

    public void ClearSlot()
    {
        DisplayItem(null, null);
    }
}