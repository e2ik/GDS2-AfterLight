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

    private Collider2D itemCollider;
    private Rigidbody2D rb;
    private bool hasBeenPickedUp = false;
    [SerializeField] private EventReference primaryPickupEvent;
    private bool markedForDestruction = false;

    private bool hasRarity;
    private ERarity rarity;

    public bool HasRarity => hasRarity;
    public ERarity Rarity => rarity;
    public event Action<ERarity> OnRarityAssigned;

    private void Awake()
    {
        EnsureComponentsCached();
        CheckUniqueOwnership();
    }

    private void Start()
    {
        if (markedForDestruction) return;
        IgnorePlayerCollision(FindPlayer());
        if (!hasRarity) AssignRarity(null);
        InitializeVisuals();
    }

    public void Initialize(InventoryItemBase newItem, ERarity? assignedRarity = null)
    {
        itemDefinition = newItem;
        CheckUniqueOwnership();
        if (markedForDestruction) return;

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
    }

    private static void SetEffect(GameObject effect, bool active)
    {
        if (effect != null) effect.SetActive(active);
    }

    public void PopOut(Vector2 forceDirection, float forceMagnitude)
    {
        EnsureComponentsCached();

        if (rb != null)
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