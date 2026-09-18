using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LootPickupDisplay : MonoBehaviour
{
    public static LootPickupDisplay Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private LootPickupEntry entryPrefab;
    [SerializeField] private Transform contentParent;

    [Header("Line Cap")]
    [SerializeField] private int maxVisibleLines = 5;

    [Header("Ordering")]
    [SerializeField] private bool newestOnTop = true;

    [Header("Timing")]
    [SerializeField] private float holdDuration = 3f;
    [SerializeField] private float fadeDuration = 1.5f;

    private readonly List<LootPickupEntry> activeEntries = new List<LootPickupEntry>();
    private readonly Stack<LootPickupEntry> pool = new Stack<LootPickupEntry>();
    private VerticalLayoutGroup layoutGroup;

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
        entry.gameObject.SetActive(false);
        entry.transform.SetParent(transform, false);
        pool.Push(entry);
    }
}