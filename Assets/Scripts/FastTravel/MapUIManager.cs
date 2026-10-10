using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("Selection Reticle")]
    [SerializeField] private RectTransform selectionReticle;
    [SerializeField, Min(1f)] private float reticlePulseScale = 1.2f;
    [SerializeField, Min(0.01f)] private float reticlePulseSpeed = 1.5f;
    [SerializeField, Min(0f)] private float reticleMoveSpeed = 20f;
    [SerializeField] private Color currentNodeReticleColor = Color.yellow;
    [SerializeField] private Color selectableReticleColor = Color.white;
    [SerializeField, Min(0f)] private float reticleColorSpeed = 15f;
    [SerializeField, Range(0f, 1f)] private float reticleMovingAlpha = 0.4f;
    [SerializeField, Min(0f)] private float reticleAlphaSpeed = 25f;

    private FastTravelNodeSO currentNode;
    private Graphic[] reticleGraphics = new Graphic[0];
    private Color reticleColor = Color.white;
    private float reticleMoveStartDistance;
    private float reticleAlphaMultiplier = 1f;
    private readonly List<Selectable> nodeSelectables = new List<Selectable>();
    private Selectable currentNodeSelectable;
    private Vector3 reticleBaseScale = Vector3.one;
    private Selectable reticleTarget;

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
        {
            closeButton.onClick.AddListener(CloseMap);

            Navigation nav = closeButton.navigation;
            nav.mode = Navigation.Mode.None;
            closeButton.navigation = nav;
        }

        if (selectionReticle != null)
        {
            reticleBaseScale = selectionReticle.localScale;
            reticleGraphics = selectionReticle.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic graphic in reticleGraphics)
                graphic.raycastTarget = false;
            selectionReticle.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (closeButton != null && IsMapOpen)
        {
            bool interactable = !IsCloseLocked;
            if (closeButton.interactable != interactable) closeButton.interactable = interactable;
        }

        UpdateReticle();
    }

    protected override Selectable GetInitialSelectable()
    {
        if (currentNodeSelectable != null) return currentNodeSelectable;

        foreach (Selectable selectable in nodeSelectables)
        {
            if (selectable != null && selectable.IsInteractable()) return selectable;
        }

        Selectable fallback = base.GetInitialSelectable();
        return fallback == closeButton ? null : fallback;
    }

    private void UpdateReticle()
    {
        if (selectionReticle == null) return;

        Selectable target = IsMapOpen ? FindSelectedNode() : null;

        if (target == null)
        {
            if (selectionReticle.gameObject.activeSelf) selectionReticle.gameObject.SetActive(false);
            reticleTarget = null;
            return;
        }

        Vector3 targetPosition = target.transform.position;
        bool snap = reticleTarget == null || !selectionReticle.gameObject.activeSelf || reticleMoveSpeed <= 0f;

        bool targetChanged = target != reticleTarget;

        if (!selectionReticle.gameObject.activeSelf) selectionReticle.gameObject.SetActive(true);
        reticleTarget = target;

        if (snap) reticleMoveStartDistance = 0f;
        else if (targetChanged) reticleMoveStartDistance = Vector3.Distance(selectionReticle.position, targetPosition);

        selectionReticle.position = snap
            ? targetPosition
            : Vector3.Lerp(selectionReticle.position, targetPosition, 1f - Mathf.Exp(-reticleMoveSpeed * Time.unscaledDeltaTime));

        float moveProgress = reticleMoveStartDistance > 0.0001f
            ? Mathf.Clamp01(Vector3.Distance(selectionReticle.position, targetPosition) / reticleMoveStartDistance)
            : 0f;
        float targetAlpha = Mathf.Lerp(1f, reticleMovingAlpha, moveProgress);
        reticleAlphaMultiplier = snap || reticleAlphaSpeed <= 0f
            ? targetAlpha
            : Mathf.Lerp(reticleAlphaMultiplier, targetAlpha, 1f - Mathf.Exp(-reticleAlphaSpeed * Time.unscaledDeltaTime));

        float t = (1f - Mathf.Cos(Time.unscaledTime * reticlePulseSpeed * Mathf.PI * 2f)) * 0.5f;
        selectionReticle.localScale = reticleBaseScale * Mathf.Lerp(1f, reticlePulseScale, t);

        Color targetColor = target == currentNodeSelectable ? currentNodeReticleColor : selectableReticleColor;
        reticleColor = snap || reticleColorSpeed <= 0f
            ? targetColor
            : Color.Lerp(reticleColor, targetColor, 1f - Mathf.Exp(-reticleColorSpeed * Time.unscaledDeltaTime));
        Color finalColor = reticleColor;
        finalColor.a *= reticleAlphaMultiplier;
        ApplyReticleColor(finalColor);
    }

    private void ApplyReticleColor(Color color)
    {
        foreach (Graphic graphic in reticleGraphics)
        {
            if (graphic != null && graphic.color != color) graphic.color = color;
        }
    }

    private Selectable FindSelectedNode()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return null;

        foreach (Selectable selectable in nodeSelectables)
        {
            if (selectable == null) continue;
            if (selected == selectable.gameObject || selected.transform.IsChildOf(selectable.transform)) return selectable;
        }

        return null;
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
        if (selectionReticle != null) selectionReticle.gameObject.SetActive(false);
        reticleTarget = null;

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
            if (selectionReticle != null && selectionReticle.IsChildOf(child)) continue;
            Destroy(child.gameObject);
        }

        nodeSelectables.Clear();
        currentNodeSelectable = null;

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

            Selectable selectable = nodeUI.GetComponentInChildren<Selectable>(true);
            if (selectable != null)
            {
                nodeSelectables.Add(selectable);
                if (isCurrent) currentNodeSelectable = selectable;
            }
        }
    }

    private void OnNodeClicked(FastTravelNodeSO targetNode)
    {
        if (IsCloseLocked) return;
        CloseMap();
        FastTravelManager.Instance.TravelTo(targetNode);
    }
}