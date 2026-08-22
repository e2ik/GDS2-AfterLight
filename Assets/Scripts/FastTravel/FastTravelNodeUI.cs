using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FastTravelNodeUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;

    private FastTravelNodeSO nodeData;
    private System.Action<FastTravelNodeSO> onSelectedCallback;

    public void Setup(FastTravelNodeSO data, bool isCurrentNode, System.Action<FastTravelNodeSO> onSelected)
    {
        nodeData = data;
        onSelectedCallback = onSelected;

        if (nameText != null) 
            nameText.text = data.displayName;

        button.interactable = !isCurrentNode;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelectedCallback?.Invoke(nodeData));
    }
}