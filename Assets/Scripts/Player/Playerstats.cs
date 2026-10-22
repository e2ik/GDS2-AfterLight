using FMODUnity;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Base Stats")]
    [SerializeField] private float baseAttack = 10f;
    [SerializeField] private float baseDefense = 5f;
    [SerializeField] private float baseHumanity = 10f;
    [SerializeField, Range(0f, 1f)] private float defenseMitigationPerPoint = 0.05f;

    [Header("Gear Stats")]
    [SerializeField] private float gearAttackBonus = 0f;
    [SerializeField] private float gearDefenseBonus = 0f;
    [SerializeField] private float gearHumanityBonus = 0f;
    [SerializeField] private float gemAttackBonus = 0f;
    [SerializeField] private float weaponAttackBonus = 0f;
    [SerializeField] private float gearCritBonus = 0f;
    [SerializeField] private float gemCritBonus = 0f;

    [Header("FMOD Events")]
    [SerializeField] private EventReference hitEvent;

    [Header("Crush Death")]
    [Tooltip("General overlap threshold (world units) for any contact with a PositionSwitch's designated crush collider.")]
    [SerializeField] private float crushPenetrationThreshold = 0.15f;
    [Tooltip("Used specifically when a PositionSwitch platform is moving DOWN.")]
    [SerializeField] private float descendingCrushThreshold = 0.02f;

    [Header("Death")]
    [SerializeField] private float deathLandingTimeout = 2f;
    [SerializeField, Min(0f)] private float deathScreenDelay = 1.5f;

    private Player player;
    private GameUI.DeathWindow deathWindow;
    private Coroutine landingSuspendRoutine;
    private bool isRespawning;
    private Coroutine deathScreenRoutine;
    private bool deathScreenShown;

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
    public event System.Action OnDamageTaken;
    public static event System.Action OnRespawnStarted;

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

    public float GetPotentialAttack()
    {
        float nonWeaponAttack = GetBestNonWeaponAttack();
        float bestWeaponAttack = 0f;
        foreach (WeaponInstance weapon in GetOwnedWeapons())
            bestWeaponAttack = Mathf.Max(bestWeaponAttack, weapon.InstRolledAttack);

        return nonWeaponAttack + bestWeaponAttack;
    }

    public float GetPotentialHitDamage() => GetPotentialHitDamage(out _, out _);

    public float GetPotentialHitDamage(out float bestAttack, out WeaponInstance bestWeapon)
    {
        float exponent = player != null && player.CombatController != null ? player.CombatController.DamageScalingExponent : 0.8f;
        float nonWeaponAttack = GetBestNonWeaponAttack();
        float best = Mathf.Pow(Mathf.Max(0f, nonWeaponAttack), exponent);
        bestAttack = nonWeaponAttack;
        bestWeapon = null;

        foreach (WeaponInstance weapon in GetOwnedWeapons())
        {
            float attack = nonWeaponAttack + weapon.InstRolledAttack;
            float hit = weapon.InstRolledDamage + Mathf.Pow(Mathf.Max(0f, attack), exponent);
            if (hit > best)
            {
                best = hit;
                bestAttack = attack;
                bestWeapon = weapon;
            }
        }

        return best;
    }

    private float GetBestNonWeaponAttack()
    {
        float bestGem = 0f;
        Dictionary<EGearSlot, float> bestGear = new Dictionary<EGearSlot, float>();

        PlayerEquipmentManager equipment = player != null ? player.Equipment : null;
        PlayerInventorySO owned = player != null && player.Inventory != null ? player.Inventory.currentInventory : null;

        if (equipment != null)
        {
            foreach (var slot in equipment.EquippedGear)
                ConsiderGear(bestGear, slot.Value);

            SecondaryGemInstance equippedGem = equipment.SecondaryGem;
            if (equippedGem != null && !string.IsNullOrEmpty(equippedGem.InstTemplateID))
                bestGem = Mathf.Max(bestGem, equippedGem.InstRolledDamageValue);
        }

        if (owned != null)
        {
            if (owned.GearInstances != null)
                foreach (GearInstance gear in owned.GearInstances) ConsiderGear(bestGear, gear);

            if (owned.SecondaryGems != null)
                foreach (SecondaryGemInstance gem in owned.SecondaryGems)
                    if (gem != null && !string.IsNullOrEmpty(gem.InstTemplateID)) bestGem = Mathf.Max(bestGem, gem.InstRolledDamageValue);
        }

        float total = baseAttack + bestGem;
        foreach (float value in bestGear.Values) total += value;
        return total;
    }

    private List<WeaponInstance> GetOwnedWeapons()
    {
        List<WeaponInstance> weapons = new List<WeaponInstance>();

        WeaponInstance equipped = player != null && player.Equipment != null ? player.Equipment.EquippedWeapon : null;
        if (equipped != null && !string.IsNullOrEmpty(equipped.InstTemplateID)) weapons.Add(equipped);

        PlayerInventorySO owned = player != null && player.Inventory != null ? player.Inventory.currentInventory : null;
        if (owned != null && owned.Weapons != null)
        {
            foreach (WeaponInstance weapon in owned.Weapons)
                if (weapon != null && !string.IsNullOrEmpty(weapon.InstTemplateID) && !weapons.Contains(weapon)) weapons.Add(weapon);
        }

        return weapons;
    }

    private static void ConsiderGear(Dictionary<EGearSlot, float> bestGear, GearInstance gear)
    {
        if (gear == null) return;

        GearDefinition def = GameDatabase.GetGearTemplateFromID(gear.InstTemplateID);
        if (def == null) return;

        bestGear.TryGetValue(def.Slot, out float current);
        if (gear.InstBonusAttack > current) bestGear[def.Slot] = gear.InstBonusAttack;
    }

    private float UpdateAttackDisplay()
    {
        return baseAttack + gearAttackBonus + gemAttackBonus + weaponAttackBonus;
    }

    public void RecalculateStats()
    {
        gearAttackBonus = 0f;
        gearDefenseBonus = 0f;
        gearHumanityBonus = 0f;
        gemAttackBonus = 0f;
        weaponAttackBonus = 0f;
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

            WeaponInstance weapon = player.Equipment.EquippedWeapon;
            if (weapon != null && !string.IsNullOrEmpty(weapon.InstTemplateID))
            {
                weaponAttackBonus = weapon.InstRolledAttack;
                gearDefenseBonus += weapon.InstRolledDefense;
            }

            var gem = player.Equipment.SecondaryGem;
            if (gem != null && !string.IsNullOrEmpty(gem.InstTemplateID))
            {
                gemAttackBonus = gem.InstRolledDamageValue;
                gemCritBonus = gem.InstRolledCritValue / 100f;
            }
        }

        OnStatsRecalculated?.Invoke();
    }

    public void TakeDamage(float rawDamage)
    {
        if (IsDead || rawDamage <= 0f) return;

        if (player != null) player.Controller.CancelClimb();

        float mitigationMultiplier = Mathf.Pow(1f - defenseMitigationPerPoint, TotalDefense);
        float effectiveDamage = Mathf.Max(1f, rawDamage * mitigationMultiplier);

        currentHealth = Mathf.Max(0f, currentHealth - effectiveDamage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke();
        Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerHit);

        AudioManager.PlaySFX(hitEvent, transform.position);

        // Debug.Log($"Actual Dmg: {rawDamage} - received dmg: {effectiveDamage} (mitigation:{(1f - mitigationMultiplier):P1}, def:{TotalDefense}). Current Health: {currentHealth}");

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Crush()
    {
        if (IsDead) return;

        currentHealth = 0f;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Die();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsDead) return;

        Collider2D hitCollider = collision.collider;

        PositionSwitch platform = hitCollider.GetComponentInParent<PositionSwitch>();
        if (platform == null || platform.CrushCollider == null) return;
        if (hitCollider != platform.CrushCollider) return;

        int contactCount = collision.contactCount;
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            bool descendingOntoPlayer = platform.CurrentVerticalDirection < -0.01f && contact.normal.y < -0.5f;
            float threshold = descendingOntoPlayer ? descendingCrushThreshold : crushPenetrationThreshold;

            if (contact.separation <= -threshold)
            {
                Crush();
                return;
            }
        }
    }

    public void ReviveFull()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        OnDied?.Invoke();
        // Debug.Log("Player died.");

        if (player != null)
        {
            player.Controller.CancelClimb();
            player.CombatController.ForceCancelAttack();
            player.CombatController.CancelParry();
            player.CombatController.EndSkill();
        }

        SetInputLocked(true);

        StopLandingWait();
        if (player != null)
            landingSuspendRoutine = StartCoroutine(WaitForLandingThenSuspend());

        deathScreenShown = false;
        if (deathScreenRoutine != null) StopCoroutine(deathScreenRoutine);
        deathScreenRoutine = StartCoroutine(ShowDeathScreenAfter(deathScreenDelay));
    }

    private IEnumerator ShowDeathScreenAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        deathScreenRoutine = null;
        ShowDeathScreen();
    }

    public void ShowDeathScreen()
    {
        if (!IsDead || deathScreenShown) return;

        if (deathScreenRoutine != null)
        {
            StopCoroutine(deathScreenRoutine);
            deathScreenRoutine = null;
        }

        deathScreenShown = true;
        GameUI.DeathWindow window = GetDeathWindow();
        if (window != null) GameUI.UIManager.Instance.Open(window);
    }

    private IEnumerator WaitForLandingThenSuspend()
    {
        float timer = 0f;
        while (player.Controller != null && !player.Controller.IsGrounded && timer < deathLandingTimeout)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        landingSuspendRoutine = null;

        if (player.Controller != null)
            player.Controller.SetPhysicsSuspended(true);
    }

    private void StopLandingWait()
    {
        if (landingSuspendRoutine == null) return;

        StopCoroutine(landingSuspendRoutine);
        landingSuspendRoutine = null;
    }

    public void OnRespawnButtonPressed()
    {
        if (isRespawning) return;
        isRespawning = true;
        OnRespawnStarted?.Invoke();

        GameUI.DeathWindow window = GetDeathWindow();
        if (window != null) GameUI.UIManager.Instance.Close(window);

        StopLandingWait();
        if (player != null) player.Controller.SetPhysicsSuspended(true);

        if (CanRespawn)
        {
            FastTravelManager.Instance.RespawnAtLastFastTravel();
        }
        else if (GameManager.Instance != null)
        {
            Debug.LogWarning("[PlayerStats] No fast travel point visited this session — respawning at the starting anchor instead.");

            GameManager.Instance.ForceReloadAndRespawnAtStart(FinishRespawn);
        }
        else
        {
            Debug.LogWarning("[PlayerStats] No fast travel point visited and no GameManager found; reviving in place.");
            FinishRespawn();
        }
    }

    private void HandleRespawnComplete()
    {
        if (!isRespawning) return;
        FinishRespawn();
    }

    private void FinishRespawn()
    {
        StopLandingWait();
        isRespawning = false;

        ReviveFull();
        if (player != null && player.Heals != null) player.Heals.ResetHeals();
        PrefabSpawner.ResetAllDefeated();
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