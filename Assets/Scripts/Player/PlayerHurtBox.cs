using System;
using Enemies;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerHurtBox : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private PlayerCombatController combatController;
    private PlayerController playerController;
    private PlayerAnimation playerAnimation;
    private Collider2D col;
    private bool manualInvulnerable;

    public static event Action<HitBox, bool> AnyAttackDodged;
    private static HitBox lastDodgedHitbox;
    private static float lastDodgedTime;
    private const float DodgeRepeatWindow = 0.5f;
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

        if (playerAnimation == null)
        {
            Player player = GetComponentInParent<Player>();
            if (player != null) playerAnimation = player.Animation;
        }

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
        if (Invulnerable)
        {
            if (!manualInvulnerable && playerController != null && playerController.IsInvulnerable)
                ReportDodge(hitbox, playerController.IsInPerfectDodgeWindow);
            return false;
        }
        if (hitbox.HasBeenParried) return false;

        bool parryWindowOpen = hitbox.SourceEvents != null && hitbox.SourceEvents.ParryWindowOpen;
        bool isUnparryable = hitbox.AttackForce == AttackForce.Heavy;

        if (parryWindowOpen && !isUnparryable && combatController != null && combatController.CheckParry(hitbox.ParryDirection))
        {
            hitbox.SetParried();
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

        hitbox.ConfirmHit();

        return true; // Successfully took the hit.
    }

    private static void ReportDodge(HitBox hitbox, bool isPerfect)
    {
        if (hitbox == null || hitbox.HasBeenParried) return;
        if (hitbox == lastDodgedHitbox && Time.time - lastDodgedTime < DodgeRepeatWindow) return;

        lastDodgedHitbox = hitbox;
        lastDodgedTime = Time.time;
        AnyAttackDodged?.Invoke(hitbox, isPerfect);
    }

    public bool TakeHazardHit(float damage, Vector2 contactPoint, float force, float staggerDuration, Vector2? directionOverride = null, bool dealsDamage = true, bool resetsPlayer = false)
    {
        if (Invulnerable) return false;
        if (stats == null || playerController == null || stats.IsDead) return false;

        if (resetsPlayer)
        {
            playerController.ResetPosition();
            return true;
        }
        if (!playerController.ApplyHazardKnockback(contactPoint, force, staggerDuration, directionOverride)) return false;
        if (!dealsDamage) return true;

        stats.TakeDamage(damage);
        PSpawner.Spawn("PlayerHit", transform.position);
        if (playerAnimation != null) playerAnimation.PlayHurtAnimation();

        return true;
    }
}