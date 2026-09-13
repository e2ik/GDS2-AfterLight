using FMODUnity;
using UnityEngine;
using System.Collections;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Base Stats")]
    [SerializeField] private float baseAttack = 10f; //using weapon base attack, not sure if this is needed since you will always have a weapon
    [SerializeField] private float baseDefense = 5f;
    [SerializeField] private float baseHumanity = 10f;
    [SerializeField, Range(0f, 1f)] private float defenseMitigationPerPoint = 0.05f; // multiplicative damage reduction per point of Defense

    [Header("Gear Stats")]
    [SerializeField] private float gearAttackBonus = 0f;
    [SerializeField] private float gearDefenseBonus = 0f;
    [SerializeField] private float gearHumanityBonus = 0f;
    [SerializeField] private float gemAttackBonus = 0f;
    [SerializeField] private float gearCritBonus = 0f;
    [SerializeField] private float gemCritBonus = 0f;

    [Header("FMOD Events")]
    [SerializeField] private EventReference hitEvent;

    private Player player;
    private GameUI.DeathWindow deathWindow;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float TotalAttack => UpdateAttackDisplay();
    public float TotalDefense => baseDefense + gearDefenseBonus;
    public float TotalHumanity => baseHumanity + gearHumanityBonus;
    public float TotalCrit => gearCritBonus + gemCritBonus;
    public bool IsDead => currentHealth <= 0f;
    public bool CanRespawn => FastTravelManager.Instance != null && FastTravelManager.Instance.HasLastVisitedNode;

    public event System.Action<float, float> OnHealthChanged;
    public event System.Action OnStatsRecalculated;
    public event System.Action OnDied;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();

        RecalculateStats();

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void OnEnable()
    {
        if (player != null)
            player.Equipment.OnEquipmentChanged += RecalculateStats;

        if (FastTravelManager.Instance != null)
            FastTravelManager.Instance.OnFastTravelComplete += HandleRespawnComplete;
    }

    private void OnDisable()
    {
        if (player != null)
            player.Equipment.OnEquipmentChanged -= RecalculateStats;

        if (FastTravelManager.Instance != null)
            FastTravelManager.Instance.OnFastTravelComplete -= HandleRespawnComplete;
    }

    private float UpdateAttackDisplay()
    {
        return baseAttack + gearAttackBonus + gemAttackBonus;
    }

    public void RecalculateStats()
    {
        gearAttackBonus = 0f;
        gearDefenseBonus = 0f;
        gearHumanityBonus = 0f;
        gemAttackBonus = 0f;
        gearCritBonus = 0f;
        gemCritBonus = 0f;

        if (player != null)
        {
            foreach (var slot in player.Equipment.EquippedGear)
            {
                GearInstance gear = slot.Value;
                if (gear != null)
                {
                    gearAttackBonus += gear.InstBonusAttack;
                    gearDefenseBonus += gear.InstBonusDefense;
                    gearHumanityBonus += gear.InstBonusHumanity;
                    gearCritBonus += gear.InstBonusCrit;
                }
            }

            var gem = player.Equipment.SecondaryGem;
            if (gem != null && !string.IsNullOrEmpty(gem.InstTemplateID))
            {
                gemAttackBonus = gem.InstRolledDamageValue;
                gemCritBonus = gem.InstRolledCritValue / 100f;
            }
        }

        OnStatsRecalculated?.Invoke();
        Debug.Log($"[PlayerStats] Stats Recalculated -> Atk: {TotalAttack} (gear:{gearAttackBonus:+#;-#;0}, gem:{gemAttackBonus:+#;-#;0}), Def: {TotalDefense}, Humanity: {TotalHumanity}, Crit: {TotalCrit:P1}");
    }

    public void TakeDamage(float rawDamage)
    {
        if (IsDead || rawDamage <= 0f) return;

        float mitigationMultiplier = Mathf.Pow(1f - defenseMitigationPerPoint, TotalDefense);
        float effectiveDamage = Mathf.Max(1f, rawDamage * mitigationMultiplier);

        currentHealth = Mathf.Max(0f, currentHealth - effectiveDamage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        AudioManager.PlaySFX(hitEvent, transform.position);

        Debug.Log($"Actual Dmg: {rawDamage} - received dmg: {effectiveDamage} (mitigation:{(1f - mitigationMultiplier):P1}, def:{TotalDefense}). Current Health: {currentHealth}");

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ReviveFull()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        OnDied?.Invoke();
        Debug.Log("Player died.");

        if (player != null)
        {
            player.CombatController.ForceCancelAttack();
            player.CombatController.CancelParry();
            player.CombatController.EndSkill();
        }

        SetInputLocked(true);

        if (player != null)
            StartCoroutine(WaitForLandingThenSuspend());

        GameUI.DeathWindow window = GetDeathWindow();
        if (window != null) GameUI.UIManager.Instance.Open(window);
    }

    private IEnumerator WaitForLandingThenSuspend()
    {
        while (player.Controller != null && !player.Controller.IsGrounded)
        {
            yield return null;
        }

        if (player.Controller != null)
            player.Controller.SetPhysicsSuspended(true);
    }

    public void OnRespawnButtonPressed()
    {
        GameUI.DeathWindow window = GetDeathWindow();
        if (window != null) GameUI.UIManager.Instance.Close(window);

        if (CanRespawn)
        {
            FastTravelManager.Instance.RespawnAtLastFastTravel();
        }
        else
        {
            Debug.LogWarning("[PlayerStats] No fast travel point visited this session; can't respawn.");
            ReviveFull();
            SetInputLocked(false);
            if (player != null) player.Controller.SetPhysicsSuspended(false);
        }
    }

    private void HandleRespawnComplete()
    {
        ReviveFull();
        SetInputLocked(false);
        if (player != null) player.Controller.SetPhysicsSuspended(false);
    }

    private void SetInputLocked(bool locked)
    {
        if (player != null)
            player.Controller.InputEnabled = !locked;
    }

    private GameUI.DeathWindow GetDeathWindow()
    {
        if (deathWindow == null)
            deathWindow = FindFirstObjectByType<GameUI.DeathWindow>();
        return deathWindow;
    }
}