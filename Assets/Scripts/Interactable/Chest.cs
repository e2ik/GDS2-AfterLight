using FMODUnity;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Chest : MonoBehaviour, IInteractable
{
    [System.Serializable]
    private class LightStateSettings
    {
        [ColorUsage(false, true)] public Color color = Color.white;
        public float intensity = 1f;
        public bool pulse = true;
        public float pulseAmount = 0.3f;
        public float pulseSpeed = 1f;
    }

    private bool isOpened = false;

    private string chestID;
    public string ChestID => chestID;

    [Header("Loot Configuration")]
    [SerializeField] private InventoryItemBase lootItem;
    [SerializeField] private WorldItem worldItemPrefab;
    [SerializeField] private LootTableDefinitionSO lootTable;

    [Header("Rarity")]
    [SerializeField] private bool useFixedRarity = false;
    [SerializeField] private ERarity fixedRarity = ERarity.Rare;
    [SerializeField] private bool overrideRarityOdds = false;
    [SerializeField] private RarityWeights rarityOdds = new RarityWeights();

    [Header("Pop Physics Settings")]
    [SerializeField] private float popForce = 5f;
    [SerializeField] private float minHorizontalAngle = -0.4f;
    [SerializeField] private float maxHorizontalAngle = 0.4f;

    [Header("Light")]
    [SerializeField] private Light2D[] lights;
    [SerializeField] private LightStateSettings closedLight = new LightStateSettings { color = new Color(1f, 0.8f, 0.4f), intensity = 0.8f, pulseAmount = 0.25f, pulseSpeed = 0.75f };
    [SerializeField] private LightStateSettings openedLight = new LightStateSettings { color = new Color(1f, 0.8f, 0.4f), intensity = 0f, pulse = false };
    [SerializeField] private float lightBlendSpeed = 4f;

    [SerializeField] private SpriteOutlineToggle outlineToggle;
    [SerializeField] private Collider2D promptCollider;

    private static readonly int IsOpenedHash = Animator.StringToHash("isOpened");
    private static readonly int IsInteractedHash = Animator.StringToHash("isInteracted");

    private Animator anim;

    private Color currentLightColor;
    private float currentLightIntensity;
    private float currentPulseAmount;
    private float currentPulseSpeed;
    private float pulsePhase;
    private bool lightInitialized;

    public string InteractionPrompt => "Open Chest";
    public bool CanInteract => !isOpened;
    public bool ShouldStopPlayerMovement => false;
    [SerializeField] private EventReference chestOpen;
    [SerializeField] private string tutorialEventKey;
    public SpriteOutlineToggle OutlineToggle => outlineToggle;
    public Collider2D PromptCollider => promptCollider;

    private void Awake()
    {
        chestID = GetHierarchyPath(transform);
        anim = GetComponent<Animator>();
        if (outlineToggle == null) outlineToggle = GetComponent<SpriteOutlineToggle>();
        if (promptCollider == null) promptCollider = GetComponent<Collider2D>();
        if (lights == null || lights.Length == 0) lights = GetComponentsInChildren<Light2D>(true);
    }

    private string GetHierarchyPath(Transform current)
    {
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path + $"[{current.GetSiblingIndex()}]";
        }
        return $"{gameObject.scene.name}:{path}[{transform.GetSiblingIndex()}]";
    }

    private void OnEnable()
    {
        if (SaveManager.Instance != null)
        {
            isOpened = SaveManager.Instance.IsChestOpened(chestID);
            if (isOpened)
            {
                ApplyOpenedVisualState();
            }
        }

        SnapLight();
    }

    private void OnDisable()
    {
        if (anim != null)
        {
            anim.SetBool(IsInteractedHash, false);
        }
    }

    private void Update()
    {
        UpdateLight();
    }

    public void Interact(Player player)
    {
        if (lootItem == null && lootTable == null)
        {
            Debug.LogWarning($"[Chest] Chest '{chestID}' opened, but no loot item is assigned!");
            CompleteOpening();
            return;
        }

        SpawnAndPopLoot();
        CompleteOpening();
    }

    private void SpawnAndPopLoot()
    {
        InventoryItemBase itemToDrop = lootItem;
        WorldItem prefabToUse = worldItemPrefab;
        RarityWeights odds = overrideRarityOdds ? rarityOdds : null;

        if (lootTable != null)
        {
            LootTableDefinitionSO.LootEntry entry = lootTable.PickEntry();
            if (entry == null || entry.lootItem == null)
            {
                AudioManager.PlaySFX(chestOpen, transform.position);
                return;
            }

            itemToDrop = entry.lootItem;
            if (entry.worldItem != null) prefabToUse = entry.worldItem;
            if (!overrideRarityOdds && lootTable.overrideRarityOdds) odds = lootTable.rarityOdds;
        }
        else
        {
            LootDropHistory.Record(lootItem);
        }

        if (prefabToUse == null)
        {
            Debug.LogError($"[Chest] WorldItemPrefab is not assigned on Chest '{chestID}'!");
            return;
        }

        Vector3 spawnPosition = transform.position + new Vector3(0f, 0.5f, 0f);

        WorldItem droppedItem = Instantiate(prefabToUse, spawnPosition, Quaternion.identity);
        ERarity? rarity = useFixedRarity ? fixedRarity : (ERarity?)null;

        droppedItem.Initialize(itemToDrop, rarity, odds);

        float randomX = Random.Range(minHorizontalAngle, maxHorizontalAngle);
        Vector2 popDirection = new Vector2(randomX, 1.0f).normalized;

        AudioManager.PlaySFX(chestOpen,transform.position);

        droppedItem.PopOut(popDirection, popForce);
    }

    private void CompleteOpening()
    {
        isOpened = true;

        Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.ChestOpened);
        Tutorial.TutorialEvents.Raise(tutorialEventKey);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.MarkChestOpened(chestID);
        }

        if (anim != null)
        {
            anim.SetBool(IsInteractedHash, true);
        }
    }

    private void ApplyOpenedVisualState()
    {
        if (anim != null)
        {
            anim.SetBool(IsOpenedHash, true);
        }
    }

    private LightStateSettings CurrentLightSettings => isOpened ? openedLight : closedLight;

    private void SnapLight()
    {
        LightStateSettings settings = CurrentLightSettings;
        currentLightColor = settings.color;
        currentLightIntensity = settings.intensity;
        currentPulseAmount = settings.pulse ? settings.pulseAmount : 0f;
        currentPulseSpeed = settings.pulseSpeed;
        lightInitialized = true;
        ApplyLight();
    }

    private void UpdateLight()
    {
        if (lights == null || lights.Length == 0) return;
        if (!lightInitialized) SnapLight();

        LightStateSettings settings = CurrentLightSettings;
        float t = 1f - Mathf.Exp(-lightBlendSpeed * Time.deltaTime);

        currentLightColor = Color.Lerp(currentLightColor, settings.color, t);
        currentLightIntensity = Mathf.Lerp(currentLightIntensity, settings.intensity, t);
        currentPulseAmount = Mathf.Lerp(currentPulseAmount, settings.pulse ? settings.pulseAmount : 0f, t);
        currentPulseSpeed = Mathf.Lerp(currentPulseSpeed, settings.pulseSpeed, t);

        pulsePhase += currentPulseSpeed * Mathf.PI * 2f * Time.deltaTime;
        if (pulsePhase > Mathf.PI * 2f) pulsePhase -= Mathf.PI * 2f;

        ApplyLight();
    }

    private void ApplyLight()
    {
        if (lights == null) return;

        float intensity = Mathf.Max(0f, currentLightIntensity + Mathf.Sin(pulsePhase) * currentPulseAmount);

        foreach (Light2D light in lights)
        {
            if (light == null) continue;
            light.color = currentLightColor;
            light.intensity = intensity;
            light.enabled = intensity > 0f;
        }
    }
}