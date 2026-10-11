using System.Collections.Generic;
using TMPro;
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
    [SerializeField] private UIFlickerOn syncFlicker;

    [Header("Destination Label")]
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text locationText;
    [SerializeField] private string defaultLabel = "SYNC";
    [SerializeField] private string defaultLocation = "NETWORK";
    [SerializeField] private string currentLocationLabel = "You are at";
    [SerializeField] private string travelLabel = "Sync Load to";
    [SerializeField] private bool underscoreSpaces = true;
    [SerializeField] private bool lowercaseLocation = true;
    [SerializeField] private bool pulseLabelAlpha = true;
    [SerializeField, Range(0f, 1f)] private float labelPulseMinAlpha = 0.8f;
    [SerializeField, Min(0.01f)] private float labelPulseSpeed = 1f;
    [SerializeField] private bool showLocationCursor = true;
    [SerializeField] private string locationCursor = "\u2588";
    [SerializeField, Min(0.05f)] private float cursorBlinkInterval = 0.5f;
    [SerializeField] private float locationPulseOffset = 0f;

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

    [Header("Crosshair Lines")]
    [SerializeField] private RectTransform crosshairArea;
    [SerializeField] private RectTransform crosshairHorizontal;
    [SerializeField] private RectTransform crosshairVertical;
    [SerializeField] private RectTransform crosshairLeft;
    [SerializeField] private RectTransform crosshairRight;
    [SerializeField] private RectTransform crosshairUp;
    [SerializeField] private RectTransform crosshairDown;
    [SerializeField, Min(0f)] private float crosshairThickness = 2f;
    [SerializeField, Min(0f)] private float crosshairGap = 0f;
    [SerializeField] private Vector2 crosshairOffset = Vector2.zero;
    [SerializeField] private bool crosshairFollowsReticleColor = true;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float crosshairAlpha = 0.5f;

    private FastTravelNodeSO currentNode;
    private Graphic[] reticleGraphics = new Graphic[0];
    private Color reticleColor = Color.white;
    private float reticleMoveStartDistance;
    private float reticleAlphaMultiplier = 1f;
    private readonly List<Selectable> nodeSelectables = new List<Selectable>();
    private Selectable currentNodeSelectable;
    private Vector3 reticleBaseScale = Vector3.one;
    private Selectable reticleTarget;
    private RectTransform[] crosshairRects = new RectTransform[0];
    private Graphic[] crosshairGraphics = new Graphic[0];
    private readonly Dictionary<Selectable, FastTravelNodeSO> nodeBySelectable = new Dictionary<Selectable, FastTravelNodeSO>();
    private Selectable labelTarget;
    private bool labelInitialized;
    private Color labelBaseColor = Color.white;
    private Color locationBaseColor = Color.white;
    private string currentLocationName = "";
    private bool cursorVisible;
    private bool cursorStateInitialized;

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

        if (labelText != null) labelBaseColor = labelText.color;
        if (locationText != null) locationBaseColor = locationText.color;

        if (selectionReticle != null)
        {
            reticleBaseScale = selectionReticle.localScale;
            reticleGraphics = selectionReticle.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic graphic in reticleGraphics)
                graphic.raycastTarget = false;
            selectionReticle.gameObject.SetActive(false);
        }

        List<RectTransform> rects = new List<RectTransform>();
        List<Graphic> lineGraphics = new List<Graphic>();
        foreach (RectTransform rect in new[] { crosshairHorizontal, crosshairVertical, crosshairLeft, crosshairRight, crosshairUp, crosshairDown })
        {
            if (rect == null) continue;
            rects.Add(rect);
            foreach (Graphic graphic in rect.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
                lineGraphics.Add(graphic);
            }
        }
        crosshairRects = rects.ToArray();
        crosshairGraphics = lineGraphics.ToArray();
        SetCrosshairVisible(false);
    }

    private void Update()
    {
        if (closeButton != null && IsMapOpen)
        {
            bool interactable = !IsCloseLocked;
            if (closeButton.interactable != interactable) closeButton.interactable = interactable;
        }

        UpdateReticle();
        UpdateDestinationLabel();
        UpdateLabelPulse();
        UpdateLocationCursor();
    }

    private void UpdateLocationCursor()
    {
        if (locationText == null || !IsMapOpen) return;

        bool visible = showLocationCursor && Mathf.Repeat(Time.unscaledTime, cursorBlinkInterval * 2f) < cursorBlinkInterval;
        if (cursorStateInitialized && visible == cursorVisible) return;

        cursorStateInitialized = true;
        cursorVisible = visible;
        RefreshLocationText();
    }

    private void RefreshLocationText()
    {
        if (locationText == null) return;

        if (!showLocationCursor || string.IsNullOrEmpty(locationCursor))
        {
            locationText.text = currentLocationName;
            return;
        }

        locationText.text = cursorVisible
            ? currentLocationName + locationCursor
            : currentLocationName + "<alpha=#00>" + locationCursor;
    }

    private void UpdateLabelPulse()
    {
        if (!pulseLabelAlpha || !IsMapOpen) return;

        if (labelText != null) labelText.color = PulseColor(labelBaseColor, 0f);
        if (locationText != null) locationText.color = PulseColor(locationBaseColor, locationPulseOffset);
    }

    private Color PulseColor(Color baseColor, float offset)
    {
        float t = 0.5f - 0.5f * Mathf.Cos((Time.unscaledTime * labelPulseSpeed + offset) * Mathf.PI * 2f);
        Color result = baseColor;
        result.a = baseColor.a * Mathf.Lerp(labelPulseMinAlpha, 1f, t);
        return result;
    }

    private void UpdateDestinationLabel()
    {
        if ((labelText == null && locationText == null) || !IsMapOpen) return;

        Selectable target = FindSelectedNode();
        if (labelInitialized && target == labelTarget) return;

        labelInitialized = true;
        labelTarget = target;

        if (target == null || !nodeBySelectable.TryGetValue(target, out FastTravelNodeSO node) || node == null)
        {
            SetDestinationLabel(defaultLabel, defaultLocation);
            return;
        }

        string nodeName = FormatLocationName(string.IsNullOrEmpty(node.displayName) ? node.nodeID : node.displayName);
        SetDestinationLabel(target == currentNodeSelectable ? currentLocationLabel : travelLabel, nodeName);
    }

    private string FormatLocationName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        name = name.Trim();
        if (underscoreSpaces) name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", "_");
        if (lowercaseLocation) name = name.ToLowerInvariant();
        return name;
    }

    private void SetDestinationLabel(string label, string location)
    {
        if (labelText != null) labelText.text = label;
        currentLocationName = location ?? "";
        RefreshLocationText();
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
            SetCrosshairVisible(false);
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
        UpdateCrosshair(finalColor);
    }

    private void SetCrosshairVisible(bool visible)
    {
        foreach (RectTransform rect in crosshairRects)
        {
            if (rect != null && rect.gameObject.activeSelf != visible) rect.gameObject.SetActive(visible);
        }
    }

    private void UpdateCrosshair(Color reticleFinalColor)
    {
        if (crosshairRects.Length == 0) return;

        RectTransform area = crosshairArea != null ? crosshairArea : mapContainer;
        if (area == null) return;

        SetCrosshairVisible(true);

        Rect bounds = area.rect;
        Vector3 reticleCenter = selectionReticle.TransformPoint(selectionReticle.rect.center);
        Vector3 local = area.InverseTransformPoint(reticleCenter);
        float x = local.x - bounds.xMin + crosshairOffset.x;
        float y = local.y - bounds.yMin + crosshairOffset.y;

        PlaceCrosshair(crosshairHorizontal, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(0f, crosshairThickness));
        PlaceCrosshair(crosshairVertical, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(crosshairThickness, 0f));
        PlaceCrosshair(crosshairLeft, Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f), new Vector2(0f, y), new Vector2(Mathf.Max(0f, x - crosshairGap), crosshairThickness));
        PlaceCrosshair(crosshairRight, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, y), new Vector2(Mathf.Max(0f, bounds.width - x - crosshairGap), crosshairThickness));
        PlaceCrosshair(crosshairDown, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f), new Vector2(x, 0f), new Vector2(crosshairThickness, Mathf.Max(0f, y - crosshairGap)));
        PlaceCrosshair(crosshairUp, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(x, 0f), new Vector2(crosshairThickness, Mathf.Max(0f, bounds.height - y - crosshairGap)));

        Color color = crosshairFollowsReticleColor ? reticleFinalColor : crosshairColor;
        color.a *= crosshairAlpha;
        foreach (Graphic graphic in crosshairGraphics)
        {
            if (graphic != null && graphic.color != color) graphic.color = color;
        }
    }

    private bool IsCrosshairUnder(Transform parent)
    {
        foreach (RectTransform rect in crosshairRects)
        {
            if (rect != null && rect.IsChildOf(parent)) return true;
        }
        return false;
    }

    private static void PlaceCrosshair(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (rect == null) return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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
        labelInitialized = false;
        labelTarget = null;
        cursorStateInitialized = false;
        SetDestinationLabel(defaultLabel, defaultLocation);
        if (syncFlicker != null) syncFlicker.Play();

        if (GameManager.Instance?.Player?.Controller != null)
        {
            GameManager.Instance.Player.Controller.SetPhysicsSuspended(true);
            GameManager.Instance.Player.Controller.InputEnabled = false;
        }
    }

    protected override void OnWindowClosed()
    {
        if (syncFlicker != null) syncFlicker.SetOff();
        if (closeButton != null) closeButton.interactable = true;
        if (selectionReticle != null) selectionReticle.gameObject.SetActive(false);
        SetCrosshairVisible(false);
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
            if (IsCrosshairUnder(child)) continue;
            Destroy(child.gameObject);
        }

        nodeSelectables.Clear();
        nodeBySelectable.Clear();
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
                nodeBySelectable[selectable] = node;
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