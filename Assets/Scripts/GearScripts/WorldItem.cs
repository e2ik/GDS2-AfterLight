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

    private Collider2D itemCollider;
    private Rigidbody2D rb;
    private bool hasBeenPickedUp = false;
    [SerializeField] private EventReference primaryPickupEvent;
    private bool markedForDestruction = false;

    private void Awake()
    {
        EnsureComponentsCached();
        CheckUniqueOwnership();
    }

    private void Start()
    {
        if (markedForDestruction) return;
        InitializeVisuals();
    }

    public void Initialize(InventoryItemBase newItem)
    {
        itemDefinition = newItem;
        CheckUniqueOwnership();
        if (markedForDestruction) return;

        InitializeVisuals();
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
        }
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
        if (itemCollider != null) itemCollider.enabled = false;
        yield return new WaitForSeconds(delay);
        if (itemCollider != null) itemCollider.enabled = true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasBeenPickedUp || itemDefinition == null) return;

        Player player = collision.gameObject.GetComponent<Player>();
        if (player == null && !collision.gameObject.CompareTag("Player")) return;

        if (player == null)
        {
            player = collision.gameObject.GetComponentInParent<Player>();
        }

        if (player != null)
        {
            CollectItem(player);
        }
    }

    private void CollectItem(Player player)
    {
        bool pickedUp = true;

        switch (itemDefinition)
        {
            case SecondaryGemBehaviourDefinition secondaryDef:
                {
                    ERarity secondaryRarity = GetWeightedRarity();
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
                    ERarity weaponRarity = GetWeightedRarity();
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
                    ERarity gearRarity = GetWeightedRarity();
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

    private ERarity GetRandomRarity()
    {
        System.Array rarities = System.Enum.GetValues(typeof(ERarity));
        return (ERarity)rarities.GetValue(Random.Range(0, rarities.Length));
    }
    private ERarity GetWeightedRarity()
    {
        float randF = Random.Range(0f,1f);

        if(randF <= 0.5f)
        {
            return ERarity.Common;
        }
        else if(randF <= 0.85)
        {
            return ERarity.Rare;
        }
        else if(randF <= 0.95)
        {
            return ERarity.Epic;
        }
        else
        {
            return ERarity.Legendary;
        }
    }
}