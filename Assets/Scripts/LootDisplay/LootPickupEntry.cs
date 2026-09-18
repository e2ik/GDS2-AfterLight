using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class LootPickupEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image borderImage;

    [Header("Border")]
    [SerializeField] private Color noRarityBorderColor = Color.white;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private LootPickupDisplay owner;

    private string itemName;
    private string tooltipBody;

    private float holdDuration;
    private float fadeDuration;
    private float spawnTime;
    private bool isHovered;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (isHovered) return;

        float elapsed = Time.time - spawnTime;

        if (elapsed <= holdDuration)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        float fadeElapsed = elapsed - holdDuration;
        float t = fadeDuration > 0f ? fadeElapsed / fadeDuration : 1f;
        canvasGroup.alpha = Mathf.Clamp01(1f - t);

        if (t >= 1f)
        {
            owner?.ReleaseEntry(this);
        }
    }

    public void Setup(LootPickupDisplay owner, Sprite icon, string itemName, ERarity? rarity, string tooltipBody, float holdDuration, float fadeDuration)
    {
        this.owner = owner;
        this.itemName = itemName;
        this.tooltipBody = tooltipBody;
        this.holdDuration = holdDuration;
        this.fadeDuration = fadeDuration;
        this.spawnTime = Time.time;
        this.isHovered = false;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText != null)
        {
            nameText.text = ItemTooltipTextBuilder.BuildLootLineText(itemName, rarity);
        }

        SetBorderColor(rarity);

        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
    }

    private void SetBorderColor(ERarity? rarity)
    {
        if (borderImage == null) return;

        borderImage.color = rarity.HasValue && GameManager.Instance != null
            ? GameManager.Instance.GetRarityColor(rarity.Value)
            : noRarityBorderColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        canvasGroup.alpha = 1f;

        if (ItemTooltip.Instance != null)
        {
            ItemTooltip.Instance.ShowTooltipAbove(itemName, tooltipBody, rectTransform);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (ItemTooltip.Instance != null)
        {
            ItemTooltip.Instance.HideTooltip();
        }
    }

    private void OnDisable()
    {
        if (isHovered && ItemTooltip.Instance != null)
        {
            ItemTooltip.Instance.HideTooltip();
        }
        isHovered = false;
    }
}