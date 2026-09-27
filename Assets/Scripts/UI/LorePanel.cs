using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LorePanel : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Complete State")]
    [SerializeField] private GameObject completeGroup;
    [SerializeField] private Image fullImage;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Locked State")]
    [SerializeField] private GameObject lockedGroup;
    [SerializeField] private TextMeshProUGUI lockedText;

    public void Show(LoreSetDisplayInfo loreSet)
    {
        gameObject.SetActive(true);

        LoreItemDefinition def = loreSet.RepresentativeInstance != null
            ? GameDatabase.GetLoreItemTemplateFromID(loreSet.RepresentativeInstance.InstItemID)
            : null;

        if (titleText != null) titleText.text = def != null ? def.UIName : "";

        bool complete = loreSet.IsComplete;
        if (completeGroup != null) completeGroup.SetActive(complete);
        if (lockedGroup != null) lockedGroup.SetActive(!complete);

        if (complete && def != null)
        {
            if (fullImage != null)
            {
                fullImage.sprite = def.FullImage;
                fullImage.enabled = def.FullImage != null;
            }

            if (bodyText != null) bodyText.text = def.ItemText;
        }
        else if (lockedText != null)
        {
            lockedText.text = $"{loreSet.OwnedCount}/{loreSet.TotalPieces} pages found.\nFind the rest to read this.";
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}