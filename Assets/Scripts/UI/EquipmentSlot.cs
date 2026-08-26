using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EquipmentSlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Sprite emptySlotSprite;

    private void Awake()
    {
        if (iconImage != null) iconImage.raycastTarget = false;
        if (nameText != null) nameText.raycastTarget = false;
    }

    public void DisplayItem(Sprite sprite, string itemName)
    {
        if (iconImage != null)
        {
            iconImage.sprite = sprite != null ? sprite : emptySlotSprite;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(itemName) ? "Empty" : itemName;
        }
    }

    public void ClearSlot()
    {
        DisplayItem(null, "Empty");
    }
}