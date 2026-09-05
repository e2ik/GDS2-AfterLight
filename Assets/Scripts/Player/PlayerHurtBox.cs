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
    public bool Invulnerable;

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
        
        TakeHit(hitbox);
    }

    public bool TakeHit(HitBox hitbox)
    {
        if (Invulnerable) return false;        

        bool parryWindowOpen = hitbox.SourceEvents != null && hitbox.SourceEvents.ParryWindowOpen;

        if (parryWindowOpen && combatController != null && combatController.CheckParry(hitbox.ParryDirection))
            return false; // Successfully parried! Did not take damage.

        bool isChargedSkillExecuting = combatController != null && combatController.IsSkilling &&
                                    (combatController.GetComponentInParent<Player>()?.Equipment?.SpecialAttackDef?.SkillExecutionType == SkillExecutionType.Charged);

        if (isChargedSkillExecuting) return false;
        
        stats.TakeDamage(hitbox.Damage);

        Vector2 sourcePosition = hitbox.transform.root.transform.position;
        if (!combatController.IsSkilling && !combatController.IsChargingSkill)
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
