using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Base Stats")]
    [SerializeField] private float baseAttack = 10f;
    [SerializeField] private float baseDefense = 5f;
    [SerializeField] private float baseHumanity = 10f;

    [Header("Gear Stats")]
    [SerializeField] private float gearAttackBonus = 0f;
    [SerializeField] private float gearDefenseBonus = 0f;
    [SerializeField] private float gearHumanityBonus = 0f;

    [Header("References")]
    [SerializeField] private PlayerEquipmentManager equipmentManager;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float TotalAttack => baseAttack + gearAttackBonus;
    public float TotalDefense => baseDefense + gearDefenseBonus;
    public float TotalHumanity => baseHumanity + gearHumanityBonus;
    public bool IsDead => currentHealth <= 0f;

    public event System.Action<float, float> OnHealthChanged;
    public event System.Action OnStatsRecalculated;
    public event System.Action OnDied;

    private void Awake()
    {
        if (equipmentManager == null)
            equipmentManager = GetComponent<PlayerEquipmentManager>();

        // Calculate stats on game load/initialization
        RecalculateStats();

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void OnEnable()
    {
        if (equipmentManager != null)
            equipmentManager.OnEquipmentChanged += RecalculateStats;
    }

    private void OnDisable()
    {
        if (equipmentManager != null)
            equipmentManager.OnEquipmentChanged -= RecalculateStats;
    }

    public void RecalculateStats()
    {
        gearAttackBonus = 0f;
        gearDefenseBonus = 0f;
        gearHumanityBonus = 0f;

        if (equipmentManager != null)
        {
            foreach (var slot in equipmentManager.EquippedGear)
            {
                GearInstance gear = slot.Value;
                if (gear != null)
                {
                    gearAttackBonus += gear.InstBonusAttack;
                    gearDefenseBonus += gear.InstBonusDefense;
                    gearHumanityBonus += gear.InstBonusHumanity;
                }
            }
        }

        OnStatsRecalculated?.Invoke();
        Debug.Log($"[PlayerStats] Stats Recalculated -> Atk: {TotalAttack} ({gearAttackBonus:+#;-#;0}), Def: {TotalDefense} ({gearDefenseBonus:+#;-#;0}), Humanity: {TotalHumanity} ({gearHumanityBonus:+#;-#;0})");
    }

    public void TakeDamage(float rawDamage)
    {
        if (IsDead || rawDamage <= 0f) return;

        float effectiveDamage = Mathf.Max(1f, rawDamage - TotalDefense);

        currentHealth = Mathf.Max(0f, currentHealth - effectiveDamage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"Player took {rawDamage} damage. Current Health: {currentHealth}");
        PlayerAnimation pAnim = GetComponent<PlayerAnimation>();
        pAnim.FlashRedOnHit();
        
        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        OnDied?.Invoke();
        Debug.Log("Player died.");
    }
}