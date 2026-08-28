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
    [Header("PlayerCombat")]
    [SerializeField] private Transform attackOrigin;
    private float attackRadius;
    public LayerMask enemyLayer;

    private float attackDamage;
    private float attackCrit; 
    [SerializeField] private float critDamageMultiplier = 2f;
    
    private bool parryPressed;
    private bool isParrying;
    [SerializeField] private float parryDuration = 0.5f;
    private float parryTimer;
    private float parryCoolDown = 0.2f;
    private ParryDirection parryDir;
    
    private bool attackPressed;
    private bool isAttacking;
    [SerializeField] private float attackDuration = 0.5f;
    private float attackTimer;
    private float attackCoolDown = 0.2f;
    
    private bool skillPressed;
    private bool skillReleased;
    private bool isSkilling;
    private float skillTimer;
    private float skillCoolDown = 0.2f;

    private float verticalInput;

    private PlayerController pController;
    private PlayerEquipmentManager equipmentManager;

    public bool IsAttacking => isAttacking;
    public bool IsParrying => isParrying;

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
    }

    private bool CanAttack()
    {
        return pController.MovementEnabled && !pController.IsWallSliding && !pController.IsChargingSkill;
    }
    
    public bool CheckParry(ParryDirection parryDirection)
    {
        return isParrying && parryDir == parryDirection;
    }
    
    private void HandleParry()
    {
        if (isParrying)
            parryTimer = parryCoolDown;
        else
            parryTimer -= Time.deltaTime;

        if (parryPressed && parryTimer <= 0f && CanAttack())
        {
            isParrying = true;
            parryDir = pController.FacingDirection == 1 ? ParryDirection.Right : ParryDirection.Left;
            if (verticalInput != 0)
            {
                if (verticalInput > 0.01f)
                    parryDir = ParryDirection.Up;
                else if (verticalInput < 0.01f && !pController.IsGrounded)
                    parryDir = ParryDirection.Down;
            }

            parryPressed = false;
            Invoke(nameof(StopParrying), parryDuration);
        }
    }
    private void StopParrying()
    {
        isParrying = false;
    }
    
    private void HandleAttack()
    {
        if (isAttacking)
            attackTimer = attackCoolDown;
        else
            attackTimer -= Time.deltaTime;

        if (attackPressed && attackTimer <= 0f && CanAttack())
        {
            isAttacking = true;
            
            if (equipmentManager.EquippedWeapon == null)
            {
                Debug.LogWarning("[PlayerController] Cannot attack: No weapon equipped in PlayerEquipmentManager.");
                return;
            }

            attackRadius = equipmentManager.EquippedWeapon.BaseWeaponRange;
            attackDamage = equipmentManager.EquippedWeapon.BaseWeaponDamage;
            attackCrit = equipmentManager.EquippedWeapon.BaseWeaponCrit;
                
            Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(attackOrigin.position, attackRadius, enemyLayer);
            
            float dmgAmount = attackDamage * (UnityEngine.Random.value <= attackCrit ? critDamageMultiplier : 1f);
            enemiesInRange[0].GetComponent<EnemyHealth>().ApplyDamage((int)dmgAmount);
            
            Debug.Log("Primary Attack executed.");
            
            attackPressed = false;
            Invoke(nameof(StopAttacking), attackDuration); //probably trigger this though animation events
        }
    }

    public void StopAttacking()
    {
        isAttacking = false;
    }
    
    private void HandleSkill()
    {
        if (isSkilling)
            skillTimer = skillCoolDown;
        else
            skillTimer -= Time.deltaTime;

        if (skillPressed && skillTimer <= 0f && CanAttack())
        {
            PrimaryGemBehaviourDefinition specialDef = equipmentManager.SpecialAttackDef;

            if (specialDef == null)
            {
                Debug.LogWarning("[PlayerController] Cannot execute Special Attack: No Primary Gem equipped.");
                return;
            }

            AttackContext context = equipmentManager.GetModifiedAttackContext();

            specialDef.Execute(context);
        }
    }
    
    public void OnMove(InputValue value)
    {
        verticalInput = value.Get<Vector2>().y;
    }

    public void OnParry()
    {
        parryPressed = true;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackOrigin.position, attackRadius);
    }
}
