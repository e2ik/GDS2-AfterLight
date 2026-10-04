using UnityEngine;
using UnityEngine.UI;

public class MapUIManager : GameUI.UIWindow
{
    public static MapUIManager Instance { get; private set; }
    public bool IsMapOpen => IsOpen;

    public static bool IsCloseLocked =>
        Tutorial.TutorialDirector.Instance != null && Tutorial.TutorialDirector.Instance.MapCloseLocked;

    public override bool CanClose => !IsCloseLocked;

    [Header("Data References")]
    [SerializeField] private WorldMapStateSO worldMapState;

    [Header("UI References")]
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private FastTravelNodeUI nodeButtonPrefab;
    [SerializeField] private Button closeButton;

    private FastTravelNodeSO currentNode;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMap);
    }

    private void Update()
    {
        if (closeButton != null && IsMapOpen)
        {
            bool interactable = !IsCloseLocked;
            if (closeButton.interactable != interactable) closeButton.interactable = interactable;
        }
    }

    public void OpenMap(FastTravelNodeSO originNode)
    {
        currentNode = originNode;
        GameUI.UIManager.Instance.Open(this);
    }

    public void CloseMap()
    {
        if (IsCloseLocked) return;
        GameUI.UIManager.Instance.Close(this);
    }

    protected override void OnWindowOpened()
    {
        RefreshMapNodes();

        if (GameManager.Instance?.Player?.Controller != null)
        {
            GameManager.Instance.Player.Controller.SetPhysicsSuspended(true);
            GameManager.Instance.Player.Controller.InputEnabled = false;
        }
    }

    protected override void OnWindowClosed()
    {
        if (closeButton != null) closeButton.interactable = true;

        if (GameManager.Instance?.Player?.Controller != null)
        {
            GameManager.Instance.Player.Controller.InputEnabled = true;
            GameManager.Instance.Player.Controller.FreezeMovement(false);
            GameManager.Instance.Player.Controller.SetPhysicsSuspended(false);
        }
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
        if (IsCloseLocked) return;
        CloseMap();
        FastTravelManager.Instance.TravelTo(targetNode);
    }
}