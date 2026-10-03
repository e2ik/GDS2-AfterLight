using System;
using System.Collections;
using UnityEngine;
using TMPro;
using FMODUnity;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class WorldItem : MonoBehaviour
{
    public enum WorldItemBehaviour
    {
        Normal,
        Unique
    }

    [Header("Item Data")]
    [SerializeField] private InventoryItemBase itemDefinition;
    [SerializeField] private WorldItemBehaviour behaviour = WorldItemBehaviour.Normal;
    // unique checks this item against the player's inventory

    [Header("Visual References")]
    [SerializeField] private SpriteRenderer itemSpriteRenderer;
    [SerializeField] private TMP_Text nameLabel;

    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 0.5f;
    private CircleCollider2D pickupTrigger;

    [Header("Rarity")]
    [SerializeField] private bool overrideRarityOdds = false;
    [SerializeField] private RarityWeights rarityOdds = new RarityWeights();
    [SerializeField] private bool colorNameByRarity = true;
    [SerializeField] private GameObject commonEffect;
    [SerializeField] private GameObject rareEffect;
    [SerializeField] private GameObject epicEffect;
    [SerializeField] private GameObject legendaryEffect;

    [Header("Rarity Glow")]
    [SerializeField] private ParticleGlow rarityGlow;
    [SerializeField] private bool useGameManagerRarityColors = true;
    [SerializeField, ColorUsage(false, false)] private Color commonGlowColor = Color.white;
    [SerializeField, ColorUsage(false, false)] private Color rareGlowColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField, ColorUsage(false, false)] private Color epicGlowColor = new Color(0.7f, 0.3f, 1f);
    [SerializeField, ColorUsage(false, false)] private Color legendaryGlowColor = new Color(1f, 0.75f, 0.2f);
    [SerializeField, Min(0f)] private float commonGlowIntensity = 1.5f;
    [SerializeField, Min(0f)] private float rareGlowIntensity = 2f;
    [SerializeField, Min(0f)] private float epicGlowIntensity = 2.5f;
    [SerializeField, Min(0f)] private float legendaryGlowIntensity = 3f;
    [SerializeField, Range(0f, 1f)] private float commonGlowOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float rareGlowOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float epicGlowOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float legendaryGlowOpacity = 1f;
    [SerializeField] private bool matchItemSorting = true;
    [SerializeField] private int glowSortingOffset = -1;

    private Collider2D itemCollider;
    private Rigidbody2D rb;
    private bool hasBeenPickedUp = false;
    [SerializeField] private EventReference primaryPickupEvent;
    private bool markedForDestruction = false;

    private bool hasRarity;
    private ERarity rarity;

    private bool isDropped;
    private AreaSide droppedAreaSide;
    private bool hiddenByArea;
    private Renderer[] areaRenderers;
    private bool[] areaRendererStates;
    private Behaviour[] areaLights;
    private bool[] areaLightStates;

    public bool HasRarity => hasRarity;
    public ERarity Rarity => rarity;
    public event Action<ERarity> OnRarityAssigned;

    private void Awake()
    {
        EnsureComponentsCached();
        CheckUniqueOwnership();
        ApplyBehaviourPhysics();
    }

    private void ApplyBehaviourPhysics()
    {
        if (rb == null || behaviour != WorldItemBehaviour.Unique) return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Start()
    {
        if (markedForDestruction) return;
        IgnorePlayerCollision(FindPlayer());
        if (!hasRarity) AssignRarity(null);
        InitializeVisuals();
    }

    private void Update()
    {
        if (!isDropped || GameManager.Instance == null) return;

        bool shouldHide = GameManager.Instance.CurrentAreaSide != droppedAreaSide;
        if (shouldHide != hiddenByArea) SetHiddenByArea(shouldHide);
    }

    private void MarkDropped()
    {
        if (isDropped || GameManager.Instance == null) return;

        isDropped = true;
        droppedAreaSide = GameManager.Instance.CurrentAreaSide;
    }

    private void SetHiddenByArea(bool hide)
    {
        if (hide)
        {
            areaRenderers = GetComponentsInChildren<Renderer>(true);
            areaRendererStates = new bool[areaRenderers.Length];
            for (int i = 0; i < areaRenderers.Length; i++)
            {
                areaRendererStates[i] = areaRenderers[i].enabled;
                areaRenderers[i].enabled = false;
            }

            areaLights = GetComponentsInChildren<UnityEngine.Rendering.Universal.Light2D>(true);
            areaLightStates = new bool[areaLights.Length];
            for (int i = 0; i < areaLights.Length; i++)
            {
                areaLightStates[i] = areaLights[i].enabled;
                areaLights[i].enabled = false;
            }
        }
        else
        {
            if (areaRenderers != null)
            {
                for (int i = 0; i < areaRenderers.Length; i++)
                    if (areaRenderers[i] != null) areaRenderers[i].enabled = areaRendererStates[i];
            }

            if (areaLights != null)
            {
                for (int i = 0; i < areaLights.Length; i++)
                    if (areaLights[i] != null) areaLights[i].enabled = areaLightStates[i];
            }
        }

        if (rb != null) rb.simulated = !hide;
        hiddenByArea = hide;
    }

    public void Initialize(InventoryItemBase newItem, ERarity? assignedRarity = null)
    {
        itemDefinition = newItem;
        CheckUniqueOwnership();
        if (markedForDestruction) return;

        MarkDropped();

        AssignRarity(assignedRarity);
        InitializeVisuals();
    }

    private void AssignRarity(ERarity? assignedRarity)
    {
        if (!RarityWeights.UsesRarity(itemDefinition))
        {
            hasRarity = false;
            return;
        }

        rarity = assignedRarity ?? RarityWeights.Roll(overrideRarityOdds, rarityOdds);
        hasRarity = true;
        OnRarityAssigned?.Invoke(rarity);
    }

    private void CheckUniqueOwnership()
    {
        if (markedForDestruction) return;
        if (behaviour != WorldItemBehaviour.Unique) return;
        if (!PlayerAlreadyOwnsItem()) return;

        markedForDestruction = true;
        hasBeenPickedUp = true;
        Destroy(gameObject);
    }

    private bool PlayerAlreadyOwnsItem()
    {
        if (itemDefinition == null) return false;

        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        PlayerInventorySO inv = player != null && player.Inventory != null ? player.Inventory.currentInventory : null;
        if (inv == null) return false;

        switch (itemDefinition)
        {
            case KeyDefinition keyDef:
                return inv.KeyInstances != null && inv.KeyInstances.Exists(k => k.InstItemID == keyDef.ItemID);

            case LoreItemDefinition loreDef:
                return inv.LoreItemInstances != null && inv.LoreItemInstances.Exists(l => l.InstItemID == loreDef.ItemID);

            default:
                Debug.LogWarning($"[WorldItem] Unique behaviour isn't implemented for item type '{itemDefinition.GetType().Name}'; treating as Normal.");
                return false;
        }
    }

    private void EnsureComponentsCached()
    {
        if (itemSpriteRenderer == null)
            itemSpriteRenderer = GetComponent<SpriteRenderer>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (itemCollider == null)
        {
            itemCollider = GetComponent<Collider2D>();
            itemCollider.isTrigger = false;
        }

        if (pickupTrigger == null)
        {
            GameObject triggerObject = new GameObject("PickupTrigger");
            triggerObject.layer = gameObject.layer;
            triggerObject.transform.SetParent(transform, false);

            pickupTrigger = triggerObject.AddComponent<CircleCollider2D>();
            pickupTrigger.isTrigger = true;
            pickupTrigger.radius = pickupRadius;
        }
    }

    private void IgnorePlayerCollision(Player player)
    {
        if (player == null || itemCollider == null) return;

        foreach (Collider2D playerCollider in player.GetComponentsInChildren<Collider2D>(true))
        {
            if (playerCollider != null)
                Physics2D.IgnoreCollision(itemCollider, playerCollider, true);
        }
    }

    private Player FindPlayer()
    {
        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        return player != null ? player : FindFirstObjectByType<Player>();
    }

    public void InitializeVisuals()
    {
        EnsureComponentsCached();

        if (itemDefinition == null) return;
        if (itemSpriteRenderer != null && itemDefinition.UISprite != null)
        {
            itemSpriteRenderer.sprite = itemDefinition.UISprite;
        }
        if (nameLabel != null)
        {
            nameLabel.text = itemDefinition.UIName;

            if (hasRarity && colorNameByRarity && GameManager.Instance != null)
                nameLabel.color = GameManager.Instance.GetRarityColor(rarity);
        }

        ApplyRarityEffects();
    }

    private void ApplyRarityEffects()
    {
        SetEffect(commonEffect, hasRarity && rarity == ERarity.Common);
        SetEffect(rareEffect, hasRarity && rarity == ERarity.Rare);
        SetEffect(epicEffect, hasRarity && rarity == ERarity.Epic);
        SetEffect(legendaryEffect, hasRarity && rarity == ERarity.Legendary);
        ApplyRarityGlow();
    }

    private void ApplyRarityGlow()
    {
        if (rarityGlow == null) return;

        GameObject glowObject = rarityGlow.gameObject;
        if (glowObject != gameObject && glowObject.activeSelf != hasRarity)
            glowObject.SetActive(hasRarity);

        if (!hasRarity) return;

        if (matchItemSorting && itemSpriteRenderer != null && rarityGlow.TryGetComponent(out ParticleSystemRenderer glowRenderer))
        {
            glowRenderer.sortingLayerID = itemSpriteRenderer.sortingLayerID;
            glowRenderer.sortingOrder = itemSpriteRenderer.sortingOrder + glowSortingOffset;
        }

        Color glowColor = GetGlowColor(rarity);
        glowColor.a = GetGlowOpacity(rarity);
        rarityGlow.SetColor(glowColor);
        rarityGlow.SetIntensity(GetGlowIntensity(rarity));

        if (rarityGlow.TryGetComponent(out ParticleSystem ps))
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private Color GetGlowColor(ERarity value)
    {
        if (useGameManagerRarityColors && GameManager.Instance != null)
            return GameManager.Instance.GetRarityColor(value);

        switch (value)
        {
            case ERarity.Rare: return rareGlowColor;
            case ERarity.Epic: return epicGlowColor;
            case ERarity.Legendary: return legendaryGlowColor;
            default: return commonGlowColor;
        }
    }

    private float GetGlowOpacity(ERarity value)
    {
        switch (value)
        {
            case ERarity.Rare: return rareGlowOpacity;
            case ERarity.Epic: return epicGlowOpacity;
            case ERarity.Legendary: return legendaryGlowOpacity;
            default: return commonGlowOpacity;
        }
    }

    private float GetGlowIntensity(ERarity value)
    {
        switch (value)
        {
            case ERarity.Rare: return rareGlowIntensity;
            case ERarity.Epic: return epicGlowIntensity;
            case ERarity.Legendary: return legendaryGlowIntensity;
            default: return commonGlowIntensity;
        }
    }

    private static void SetEffect(GameObject effect, bool active)
    {
        if (effect != null) effect.SetActive(active);
    }

    public void PopOut(Vector2 forceDirection, float forceMagnitude)
    {
        EnsureComponentsCached();
        MarkDropped();

        if (rb != null && behaviour != WorldItemBehaviour.Unique)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(forceDirection.normalized * forceMagnitude, ForceMode2D.Impulse);
        }

        StartCoroutine(EnablePickupDelay(0.4f));
    }

    private IEnumerator EnablePickupDelay(float delay)
    {
        if (pickupTrigger != null) pickupTrigger.enabled = false;
        yield return new WaitForSeconds(delay);
        if (pickupTrigger != null) pickupTrigger.enabled = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Player player = collision.gameObject.GetComponentInParent<Player>();
        if (player != null) IgnorePlayerCollision(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenPickedUp || itemDefinition == null) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;

        CollectItem(player);
    }

    private ERarity GetPickupRarity()
    {
        if (!hasRarity) AssignRarity(null);
        return rarity;
    }

    private void CollectItem(Player player)
    {
        bool pickedUp = true;

        switch (itemDefinition)
        {
            case SecondaryGemBehaviourDefinition secondaryDef:
                {
                    ERarity secondaryRarity = GetPickupRarity();
                    SecondaryGemInstance gemLoot = secondaryDef.CreateInstance(secondaryRarity);
                    SecondaryGemInstance previouslyEquippedGem = !player.Equipment.IsSecondaryGemSlotEmpty() ? player.Equipment.SecondaryGem : null;

                    if (!player.Inventory.AddItemToInventory(gemLoot))
                    {
                        pickedUp = false;
                        ShowInventoryFullMessage(secondaryDef);
                        break;
                    }

                    if (player.Equipment.IsSecondaryGemSlotEmpty())
                        player.Equipment.EquipSecondaryGem(gemLoot);

                    LootPickupDisplay.Instance?.AddPickup(
                        secondaryDef.UISprite, secondaryDef.UIName, secondaryRarity,
                        ItemTooltipTextBuilder.BuildSecondaryGemTooltip(gemLoot, previouslyEquippedGem),
                        gemLoot);

                    break;
                }

            case PrimaryGemBehaviourDefinition primaryDef:
                {
                    PrimaryGemInstance primaryLoot = primaryDef.CreateInstance();

                    if (!player.Inventory.AddItemToInventory(primaryLoot))
                    {
                        pickedUp = false;
                        ShowInventoryFullMessage(primaryDef);
                        break;
                    }

                    AudioManager.PlaySFX(primaryPickupEvent, transform.position);

                    if (player.Equipment.IsSpecialAttackSlotEmpty())
                        player.Equipment.EquipSpecialAttack(primaryDef);

                    LootPickupDisplay.Instance?.AddPickup(
                        primaryDef.UISprite, primaryDef.UIName, null,
                        ItemTooltipTextBuilder.BuildPrimaryGemTooltip(primaryDef),
                        primaryLoot);

                    break;
                }

            case WeaponDefinition weaponDef:
                {
                    ERarity weaponRarity = GetPickupRarity();
                    WeaponInstance weaponLoot = weaponDef.CreateInstance(weaponRarity);
                    WeaponInstance previouslyEquippedWeapon = player.Equipment.EquippedWeapon;

                    if (!player.Inventory.AddItemToInventory(weaponLoot))
                    {
                        pickedUp = false;
                        ShowInventoryFullMessage(weaponDef);
                        break;
                    }

                    if (player.Equipment.IsWeaponSlotEmpty())
                        player.Equipment.EquipWeapon(weaponLoot);

                    LootPickupDisplay.Instance?.AddPickup(
                        weaponDef.UISprite, weaponDef.UIName, weaponRarity,
                        ItemTooltipTextBuilder.BuildWeaponTooltip(weaponLoot, previouslyEquippedWeapon),
                        weaponLoot);

                    break;
                }

            case GearDefinition gearDef:
                {
                    ERarity gearRarity = GetPickupRarity();
                    GearInstance gearLoot = gearDef.CreateInstance(gearRarity);
                    GearInstance previouslyEquippedGear = player.Equipment.GetEquippedGear(gearDef.Slot);

                    if (!player.Inventory.AddItemToInventory(gearLoot))
                    {
                        pickedUp = false;
                        ShowInventoryFullMessage(gearDef);
                        break;
                    }

                    if (player.Equipment.IsGearSlotEmpty(gearDef.Slot))
                        player.Equipment.EquipGear(gearDef.Slot, gearLoot);

                    LootPickupDisplay.Instance?.AddPickup(
                        gearDef.UISprite, gearDef.UIName, gearRarity,
                        ItemTooltipTextBuilder.BuildGearTooltip(gearLoot, gearDef.Slot.ToString(), previouslyEquippedGear),
                        gearLoot);

                    break;
                }

            case KeyDefinition keyDef:
                {
                    KeyInstance keyLoot = keyDef.CreateInstance();

                    if (!player.Inventory.AddItemToInventory(keyLoot))
                    {
                        pickedUp = false;
                        ShowInventoryFullMessage(keyDef);
                        break;
                    }

                    LootPickupDisplay.Instance?.AddPickup(
                        keyDef.UISprite, keyDef.UIName, null,
                        keyDef.Description,
                        keyLoot);

                    break;
                }

            case LoreItemDefinition loreDef:
                {
                    LoreItemInstance loreLoot = loreDef.CreateInstance();

                    if (!player.Inventory.AddItemToInventory(loreLoot))
                    {
                        pickedUp = false;
                        ShowInventoryFullMessage(loreDef);
                        break;
                    }

                    LootPickupDisplay.Instance?.AddPickup(
                        loreDef.UISprite, loreDef.UIName, null,
                        loreDef.Description,
                        loreLoot);

                    break;
                }

            default:
                Debug.LogWarning($"[WorldItem] Item type '{itemDefinition.GetType().Name}' is not handled.");
                break;
        }

        if (pickedUp)
        {
            hasBeenPickedUp = true;
            Destroy(gameObject);
        }
        else
        {
            hasBeenPickedUp = false;
        }
    }

    private void ShowInventoryFullMessage(InventoryItemBase def)
    {
        LootPickupDisplay.Instance?.AddPickup(
            def.UISprite, "Inventory Full", null,
            $"Not enough room for {def.UIName}.");
    }
}