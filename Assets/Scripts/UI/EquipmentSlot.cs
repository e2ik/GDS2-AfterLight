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

    private Vector3 originalIconScale = Vector3.one;
    private string defaultSlotName;

    private void Awake()
    {
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

    public void DisplayItem(Sprite sprite, string itemName)
    {
        bool hasItem = sprite != null;

        if (iconImage != null)
        {
            Sprite activeSprite = hasItem ? sprite : emptySlotSprite;

            iconImage.sprite = activeSprite;
            iconImage.enabled = (activeSprite != null);

            if (activeSprite != null)
            {
                iconImage.color = Color.white;
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
    }

    public void ClearSlot()
    {
        DisplayItem(null, null);
    }
}