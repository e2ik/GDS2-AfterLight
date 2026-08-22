using UnityEngine;
using UnityEngine.UI;

public class MapUIManager : MonoBehaviour
{
    public static MapUIManager Instance { get; private set; }

    [Header("Data References")]
    [SerializeField] private WorldMapStateSO worldMapState;

    [Header("UI References")]
    [SerializeField] private GameObject mapWindow;
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

        mapWindow.SetActive(false);
    }

    public void OpenMap(FastTravelNodeSO originNode)
    {
        currentNode = originNode;
        mapWindow.SetActive(true);
        RefreshMapNodes();
    }

    public void CloseMap()
    {
        mapWindow.SetActive(false);
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