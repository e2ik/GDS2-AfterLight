using UnityEngine;
using UnityEngine.UI;

public class MapUIManager : MonoBehaviour
{
    public static MapUIManager Instance { get; private set; }
    public bool IsMapOpen { get; private set; }

    [Header("Data References")]
    [SerializeField] private WorldMapStateSO worldMapState;

    [Header("UI References")]
    [SerializeField] private UIWindowAnimator mapWindowAnimator; 
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private FastTravelNodeUI nodeButtonPrefab;
    [SerializeField] private Button closeButton;

    private FastTravelNodeSO currentNode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMap);
    }

    public void OpenMap(FastTravelNodeSO originNode)
    {
        IsMapOpen = true;
        currentNode = originNode;
        RefreshMapNodes();
        
        if (mapWindowAnimator != null)
            mapWindowAnimator.Show(true);
    }

    public void CloseMap()
    {
        IsMapOpen = false;
        GameManager.Instance.Player.Controller.FreezeMovement(false);
        if (mapWindowAnimator != null)
            mapWindowAnimator.Hide();
    }

    private void RefreshMapNodes()
    {
        foreach (Transform child in mapContainer)
        {
            Destroy(child.gameObject);
        }

        Vector2 containerSize = mapContainer.rect.size;

        foreach (var node in worldMapState.UnlockedNodes)
        {
            FastTravelNodeUI nodeUI = Instantiate(nodeButtonPrefab, mapContainer);

            RectTransform rect = nodeUI.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(
                (node.mapUIPosition.x - 0.5f) * containerSize.x,
                (node.mapUIPosition.y - 0.5f) * containerSize.y
            );

            bool isCurrent = (currentNode != null && node == currentNode);
            nodeUI.Setup(node, isCurrent, OnNodeClicked);
        }
    }

    private void OnNodeClicked(FastTravelNodeSO targetNode)
    {
        CloseMap();
        FastTravelManager.Instance.TravelTo(targetNode);
    }
}