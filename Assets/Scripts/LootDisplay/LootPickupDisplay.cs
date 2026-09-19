using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class LootPickupDisplay : MonoBehaviour
{
    public static LootPickupDisplay Instance { get; private set; }

    private readonly struct PickupData
    {
        public readonly string Name;
        public readonly string Body;

        public PickupData(string name, string body)
        {
            Name = name;
            Body = body;
        }
    }

    [Header("Setup")]
    [SerializeField] private LootPickupEntry entryPrefab;
    [SerializeField] private Transform contentParent;
    [SerializeField] private RectTransform tooltipDock;
    public RectTransform TooltipDock => tooltipDock;

    [Header("Line Cap")]
    [SerializeField] private int maxVisibleLines = 5;

    [Header("Ordering")]
    [SerializeField] private bool newestOnTop = true;

    [Header("Timing")]
    [SerializeField] private float holdDuration = 3f;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Controller Inspect")]
    [SerializeField] private InputActionReference inspectAction;
    [SerializeField] private float inspectDuration = 4f;
    [SerializeField] private TooltipAnchorSettings inspectAnchor = new TooltipAnchorSettings();

    public TooltipAnchorSettings TooltipAnchor => inspectAnchor;

    private readonly List<LootPickupEntry> activeEntries = new List<LootPickupEntry>();
    private readonly Stack<LootPickupEntry> pool = new Stack<LootPickupEntry>();
    private readonly Dictionary<LootPickupEntry, PickupData> entryData = new Dictionary<LootPickupEntry, PickupData>();
    private VerticalLayoutGroup layoutGroup;

    private LootPickupEntry inspectedEntry;
    private float inspectTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyOrdering();
    }

    private void OnEnable()
    {
        if (inspectAction == null) return;

        if (!inspectAction.action.enabled) inspectAction.action.Enable();
        inspectAction.action.performed += HandleInspect;
    }

    private void OnDisable()
    {
        if (inspectAction != null)
        {
            inspectAction.action.performed -= HandleInspect;
        }

        EndInspect();
    }

    private void Update()
    {
        if (inspectedEntry == null) return;

        inspectTimer -= Time.unscaledDeltaTime;
        if (inspectTimer <= 0f || InputModeTracker.IsUsingMouse)
        {
            EndInspect();
        }
    }

    private void ApplyOrdering()
    {
        if (layoutGroup == null && contentParent != null)
        {
            layoutGroup = contentParent.GetComponent<VerticalLayoutGroup>();
        }

        if (layoutGroup == null)
        {
            Debug.LogWarning("[LootPickupDisplay] contentParent has no VerticalLayoutGroup — can't control stack order.");
            return;
        }

        layoutGroup.reverseArrangement = newestOnTop;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyOrdering();
    }
#endif

    public void AddPickup(Sprite icon, string itemName, ERarity? rarity, string tooltipBody)
    {
        if (entryPrefab == null || contentParent == null)
        {
            Debug.LogWarning("[LootPickupDisplay] Missing entryPrefab or contentParent reference.");
            return;
        }

        LootPickupEntry entry = GetPooledEntry();
        entry.transform.SetParent(contentParent, false);
        entry.transform.SetAsLastSibling();
        entry.Setup(this, icon, itemName, rarity, tooltipBody, holdDuration, fadeDuration);

        entryData[entry] = new PickupData(itemName, tooltipBody);
        activeEntries.Insert(0, entry);

        while (activeEntries.Count > maxVisibleLines)
        {
            LootPickupEntry oldest = activeEntries[activeEntries.Count - 1];
            activeEntries.RemoveAt(activeEntries.Count - 1);
            ReturnToPool(oldest);
        }
    }

    public void ReleaseEntry(LootPickupEntry entry)
    {
        if (activeEntries.Remove(entry))
        {
            ReturnToPool(entry);
        }
    }

    private void HandleInspect(InputAction.CallbackContext context)
    {
        if (activeEntries.Count == 0) return;
        if (GameUI.UIManager.Instance != null && GameUI.UIManager.Instance.IsInputLocked) return;

        int current = inspectedEntry != null ? activeEntries.IndexOf(inspectedEntry) : -1;
        ShowInspect(activeEntries[(current + 1) % activeEntries.Count]);
    }

    private void ShowInspect(LootPickupEntry entry)
    {
        if (ItemTooltip.Instance == null) return;
        if (!entryData.TryGetValue(entry, out PickupData data)) return;

        if (inspectedEntry != null && inspectedEntry != entry)
        {
            inspectedEntry.SetInspected(false);
        }

        inspectedEntry = entry;
        inspectTimer = inspectDuration;
        entry.SetInspected(true);

        InputModeTracker.ForceNonMouse();
        ItemTooltip.Instance.ShowTooltipAnchored(data.Name, data.Body, (RectTransform)entry.transform, inspectAnchor, tooltipDock);
    }

    private void EndInspect()
    {
        if (inspectedEntry == null) return;

        if (ItemTooltip.Instance != null)
        {
            ItemTooltip.Instance.HideTooltip((RectTransform)inspectedEntry.transform);
        }

        inspectedEntry.SetInspected(false);
        inspectedEntry = null;
    }

    private LootPickupEntry GetPooledEntry()
    {
        if (pool.Count > 0)
        {
            LootPickupEntry pooled = pool.Pop();
            pooled.gameObject.SetActive(true);
            return pooled;
        }

        return Instantiate(entryPrefab);
    }

    private void ReturnToPool(LootPickupEntry entry)
    {
        if (entry == inspectedEntry) EndInspect();

        entryData.Remove(entry);
        entry.gameObject.SetActive(false);
        entry.transform.SetParent(transform, false);
        pool.Push(entry);
    }
}