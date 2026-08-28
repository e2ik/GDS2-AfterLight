using System;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

public enum ParryDirection
{
    Up,
    Down,
    Left,
    Right
}

public class PlayerCombatController : MonoBehaviour
{
    [Header("Parry Settings")]
    // actual parry window duration
    [SerializeField] private float parryActiveDuration = 0.2f;

    // if we want to implement this to parry bad timing
    [SerializeField] private float parryRecoveryDuration = 0.3f;

    // how long a parry button press stays queued in memory if pressed early
    [SerializeField] private float parryBufferTime = 0.15f;

    private float parryActiveTimer;
    private float parryRecoveryTimer;
    private float parryBufferTimer;

    private bool isParrying;           // True during Active Window only
    private bool isParryInRecovery;    // True during Recovery Window only
    private ParryDirection parryDir;

    [Header("Attack Settings")]
    [SerializeField] private Transform attackOrigin;
    public LayerMask enemyLayer;
    [SerializeField] private float critDamageMultiplier = 2f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float attackCoolDown = 0.2f;

    private float attackRadius;
    private float attackDamage;
    private float attackCrit;
    private float attackTimer;
    private bool attackPressed;
    private bool isAttacking;

    [Header("Skill Settings")]
    [SerializeField] private float skillCoolDown = 0.2f;
    private float skillTimer;
    private bool skillPressed;
    private bool skillReleased;
    private bool isSkilling;

    private float verticalInput;
    private PlayerController pController;
    private PlayerEquipmentManager equipmentManager;

    public bool IsAttacking => isAttacking;
    public bool IsParrying => isParrying || isParryInRecovery; // just for now we can make visual distinction later
    // public bool IsParryInRecovery => isParryInRecovery;

    private void Awake()
    {
        pController = GetComponent<PlayerController>();
        equipmentManager = GetComponent<PlayerEquipmentManager>();
    }

    private void Update()
    {
        if (!pController.MovementEnabled) return;

        HandleParry();
        HandleAttack();
        HandleSkill();
        UpdateTimers();
    }

    private void UpdateTimers()
    {
        // Countdown Input Buffer
        if (parryBufferTimer > 0f)
            parryBufferTimer -= Time.deltaTime;

        // Active Parry Window
        if (isParrying)
        {
            parryActiveTimer -= Time.deltaTime;
            if (parryActiveTimer <= 0f)
            {
                isParrying = false;
                isParryInRecovery = true;
                parryRecoveryTimer = parryRecoveryDuration;
            }
        }

        // Parry Recovery Window
        if (isParryInRecovery)
        {
            parryRecoveryTimer -= Time.deltaTime;
            if (parryRecoveryTimer <= 0f)
            {
                isParryInRecovery = false;
            }
        }
    }

    private bool CanAct()
    {
        return pController.MovementEnabled 
            && !pController.IsWallSliding 
            && !pController.IsChargingSkill 
            && !isParrying 
            && !isParryInRecovery 
            && !isAttacking;
    }

    #region Parry Logic

    private void HandleParry()
    {
        if (parryBufferTimer > 0f && CanAct())
        {
            ExecuteParry();
        }
    }

    private void ExecuteParry()
    {
        parryBufferTimer = 0f; // Consume buffer

        isParrying = true;
        isParryInRecovery = false;
        parryActiveTimer = parryActiveDuration;

        // Determine Direction
        parryDir = pController.FacingDirection == 1 ? ParryDirection.Right : ParryDirection.Left;

        if (verticalInput > 0.01f)
        {
            parryDir = ParryDirection.Up;
        }
        else if (verticalInput < -0.01f && !pController.IsGrounded)
        {
            parryDir = ParryDirection.Down;
        }

        Debug.Log($"Parry Executed! Direction: {parryDir}");
    }

    public bool CheckParry(ParryDirection incomingDirection)
    {
        if (isParrying && parryDir == incomingDirection)
        {
            OnSuccessfulParry();
            return true;
        }
        return false;
    }

    public void OnSuccessfulParry()
    {
        // Cancel recovery/active timers on success so player can act immediately
        isParrying = false;
        isParryInRecovery = false;
        parryActiveTimer = 0f;
        parryRecoveryTimer = 0f;

        Debug.Log("PARRY SUCCESSFUL!");
    }

    #endregion

    #region Attack Logic

    private void HandleAttack()
    {
        if (isAttacking)
            attackTimer = attackCoolDown;
        else
            attackTimer -= Time.deltaTime;

        if (attackPressed && attackTimer <= 0f && CanAct())
        {
            if (equipmentManager.EquippedWeapon == null)
            {
                Debug.LogWarning("[PlayerCombatController] Cannot attack: No weapon equipped.");
                attackPressed = false;
                return;
            }

            isAttacking = true;

            attackRadius = equipmentManager.EquippedWeapon.BaseWeaponRange;
            attackDamage = equipmentManager.EquippedWeapon.BaseWeaponDamage;
            attackCrit = equipmentManager.EquippedWeapon.BaseWeaponCrit;
                
            Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(attackOrigin.position, attackRadius, enemyLayer);
            
            // quick fix to stop compiler from complaining
            if (enemiesInRange.Length > 0)
            {
                float dmgAmount = attackDamage * (UnityEngine.Random.value <= attackCrit ? critDamageMultiplier : 1f);
                if (enemiesInRange[0].TryGetComponent(out EnemyHealth enemyHealth))
                {
                    enemyHealth.ApplyDamage((int)dmgAmount);
                }
            }
            
            Debug.Log("Primary Attack executed.");
            
            attackPressed = false;
            Invoke(nameof(StopAttacking), attackDuration);
        }
    }

    public void StopAttacking()
    {
        isAttacking = false;
    }

    #endregion

    #region Skill Logic

    private void HandleSkill()
    {
        if (isSkilling)
            skillTimer = skillCoolDown;
        else
            skillTimer -= Time.deltaTime;

        if (skillPressed && skillTimer <= 0f && CanAct())
        {
            PrimaryGemBehaviourDefinition specialDef = equipmentManager.SpecialAttackDef;

            if (specialDef == null)
            {
                Debug.LogWarning("[PlayerCombatController] Cannot execute Special Attack: No Primary Gem equipped.");
                skillPressed = false;
                return;
            }

            AttackContext context = equipmentManager.GetModifiedAttackContext();
            specialDef.Execute(context);
        }
    }

    #endregion

    #region Input Handlers

    public void OnMove(InputValue value)
    {
        verticalInput = value.Get<Vector2>().y;
    }

    public void OnParry()
    {
        parryBufferTimer = parryBufferTime;
    }

    public void OnAttack()
    {
        attackPressed = true;
    }

    public void OnSAttack(InputValue value)
    {
        if (value.isPressed)
        {
            skillPressed = true;
            skillReleased = false;
        }
        else
        {
            skillPressed = false;
            skillReleased = true;
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (attackOrigin == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackOrigin.position, attackRadius);
    }
}