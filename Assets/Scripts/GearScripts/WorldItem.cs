using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class WorldItem : MonoBehaviour
{
    [Header("Item Data")]
    [SerializeField] private InventoryItemBase itemDefinition;

    [Header("Visual References")]
    [SerializeField] private SpriteRenderer itemSpriteRenderer;
    [SerializeField] private TMP_Text nameLabel;

    private Collider2D itemCollider;
    private Rigidbody2D rb;
    private bool hasBeenPickedUp = false;

    private void Awake()
    {
        EnsureComponentsCached();
    }

    private void Start()
    {
        InitializeVisuals();
    }

    public void Initialize(InventoryItemBase newItem)
    {
        itemDefinition = newItem;
        InitializeVisuals();
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
        hasBeenPickedUp = true;

        switch (itemDefinition)
        {
            case SecondaryGemBehaviourDefinition secondaryDef:
                ERarity secondaryRarity = GetWeightedRarity();
                SecondaryGemInstance gemLoot = secondaryDef.CreateInstance(secondaryRarity);
                player.Inventory.AddItemToInventory(gemLoot);

                if (player.Equipment.IsSecondaryGemSlotEmpty())
                    player.Equipment.EquipSecondaryGem(gemLoot);

                Debug.Log($"Picked up Secondary Gem: {secondaryDef.UIName} ({secondaryRarity})");
                break;

            case PrimaryGemBehaviourDefinition primaryDef:
                PrimaryGemInstance primaryLoot = primaryDef.CreateInstance();
                player.Inventory.AddItemToInventory(primaryLoot);

                if (player.Equipment.IsSpecialAttackSlotEmpty())
                    player.Equipment.EquipSpecialAttack(primaryDef);

                Debug.Log($"Picked up Primary Gem: {primaryDef.UIName}");
                break;

            case WeaponDefinition weaponDef:
                // WeaponInstance weaponLoot = weaponDef.CreateInstance(); <-- needs implementation
                // player.Inventory.AddItemToInventory(weaponLoot);
                //
                // if (player.Equipment.IsWeaponSlotEmpty())
                //     player.Equipment.EquipWeapon(weaponDef);
                Debug.Log($"Picked up Weapon: {weaponDef.UIName}");
                break;

            case GearDefinition gearDef:
                ERarity gearRarity = GetWeightedRarity();
                GearInstance gearLoot = gearDef.CreateInstance(gearRarity);
                player.Inventory.AddItemToInventory(gearLoot);

                if (player.Equipment.IsGearSlotEmpty(gearDef.Slot))
                    player.Equipment.EquipGear(gearDef.Slot, gearLoot);

                Debug.Log($"Picked up Gear: {gearDef.UIName} ({gearRarity})");
                break;

            default:
                Debug.LogWarning($"[WorldItem] Item type '{itemDefinition.GetType().Name}' is not handled.");
                break;
        }

        Destroy(gameObject);
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