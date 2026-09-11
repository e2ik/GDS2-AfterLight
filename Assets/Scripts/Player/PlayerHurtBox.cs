using System;
using Enemies;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerHurtBox : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private PlayerCombatController combatController;
    private PlayerController playerController;
    private Collider2D col;
    private bool manualInvulnerable;
    public bool Invulnerable 
    { 
        get => manualInvulnerable || (playerController != null && playerController.IsInvulnerable);
        set => manualInvulnerable = value;
    }

    private void Awake()
    {
        if (stats == null)
            stats = GetComponentInParent<PlayerStats>();

        if (combatController == null)
            combatController = GetComponentInParent<PlayerCombatController>();

        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        if (col == null)
            col = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var hitbox = other.GetComponent<HitBox>();
        if (hitbox == null || !hitbox.IsActive) return;
        
        TakeHit(hitbox, other);
    }

    public bool TakeHit(HitBox hitbox, Collider2D col2d = null)
    {
        if (Invulnerable) return false;        

        bool parryWindowOpen = hitbox.SourceEvents != null && hitbox.SourceEvents.ParryWindowOpen;
        bool isUnparryable = hitbox.AttackForce == AttackForce.Heavy;

        if (parryWindowOpen && !isUnparryable && combatController != null && combatController.CheckParry(hitbox.ParryDirection))
        {
            combatController.TryModifyParry(hitbox.Damage, col2d); //trigger secondary gem effect
            return false; // Successfully parried! Did not take damage.
        }

        bool isChargedSkillExecuting = combatController != null && combatController.IsSkilling &&
                                    (combatController.GetComponentInParent<Player>()?.Equipment?.SpecialAttackDef?.SkillExecutionType == SkillExecutionType.Charged);

        if (isChargedSkillExecuting) return false;
        
        stats.TakeDamage(hitbox.Damage);
        PSpawner.Spawn("PlayerHit", transform.position);

        Vector2 sourcePosition = hitbox.transform.root.transform.position;
        if (!combatController.IsSkilling && !combatController.IsChargeInputHeld)
        {
            playerController.ApplyKnockback(sourcePosition, hitbox.AttackForce);
        }
        else
        {
            combatController.CancelSkillStates();
            combatController.EndSkill();
            playerController.ApplyKnockback(sourcePosition, hitbox.AttackForce);
        }

        return true; // Successfully took the hit.
    }
}
