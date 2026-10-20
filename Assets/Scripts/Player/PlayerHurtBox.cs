using System;
using Enemies;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerHurtBox : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private PlayerCombatController combatController;

    [Header("Late Parry")]
    [SerializeField, Min(0f)] private float lateParryGrace = 0.1f;
    [SerializeField] private bool lateParryProjectilesOnly = true;
    private PlayerController playerController;
    private PlayerAnimation playerAnimation;
    private Collider2D col;
    private bool manualInvulnerable;

    private HitBox pendingHitbox;
    private Collider2D pendingCollider;
    private bool pendingIgnoreDirection;
    private float pendingTimer;

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

    private void OnDisable()
    {
        pendingHitbox = null;
        pendingCollider = null;
    }

    private void Update()
    {
        if (pendingHitbox == null) return;

        HitBox hitbox = pendingHitbox;

        if (hitbox.HasBeenParried || hitbox.HasHit || stats == null || stats.IsDead)
        {
            pendingHitbox = null;
            return;
        }

        if (Invulnerable)
        {
            pendingHitbox = null;
            if (!manualInvulnerable && playerController != null && playerController.IsInvulnerable)
                ReportDodge(hitbox, playerController.IsInPerfectDodgeWindow);
            return;
        }

        if (combatController != null && combatController.CheckParry(hitbox.ParryDirection, pendingIgnoreDirection))
        {
            pendingHitbox = null;
            ConfirmAvoidedHit(hitbox);
            hitbox.SetParried();
            combatController.TryModifyParry(hitbox.Damage, pendingCollider);
            return;
        }

        pendingTimer -= Time.deltaTime;
        if (pendingTimer > 0f) return;

        pendingHitbox = null;
        ApplyHit(hitbox);
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
        if (hitbox.HasBeenParried || hitbox.HasHit) return false;

        playerController.CancelHeal();
        bool parryWindowOpen = hitbox.SourceEvents != null && hitbox.SourceEvents.ParryWindowOpen;
        bool isUnparryable = hitbox.AttackForce == AttackForce.Heavy && (combatController == null || !combatController.CanParryHeavy);
        bool isProjectile = hitbox.GetComponentInParent<Enemies.ProjectileScripts.Projectile>() != null;
        bool ignoreParryDirection = hitbox.IgnoreParryDirection
                                    || (GameManager.Instance != null && GameManager.Instance.ProjectilesIgnoreParryDirection && isProjectile);

        if (parryWindowOpen && !isUnparryable && combatController != null && combatController.CheckParry(hitbox.ParryDirection, ignoreParryDirection))
        {
            ConfirmAvoidedHit(hitbox);
            hitbox.SetParried();
            combatController.TryModifyParry(hitbox.Damage, col2d); //trigger secondary gem effect

            return false; // Successfully parried! Did not take damage.
        }

        bool canLateParry = lateParryGrace > 0f && parryWindowOpen && !isUnparryable && combatController != null
                            && pendingHitbox == null && (isProjectile || !lateParryProjectilesOnly);

        if (canLateParry)
        {
            pendingHitbox = hitbox;
            pendingCollider = col2d;
            pendingIgnoreDirection = ignoreParryDirection;
            pendingTimer = lateParryGrace;
            return false;
        }

        return ApplyHit(hitbox);
    }

    private bool ApplyHit(HitBox hitbox)
    {
        bool isChargedSkillExecuting = combatController != null && combatController.IsSkilling &&
                                    (combatController.GetComponentInParent<Player>()?.Equipment?.SpecialAttackDef?.SkillExecutionType == SkillExecutionType.Charged);

        if (isChargedSkillExecuting) return false;

        hitbox.MarkHit();

        bool hyperArmor = combatController != null && combatController.HasAttackHyperArmor;

        stats.TakeDamage(hitbox.Damage);
        PSpawner.Spawn("PlayerHit", transform.position);

        if (hyperArmor)
        {
            hitbox.ConfirmHit();
            return true;
        }

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

    private static void ConfirmAvoidedHit(HitBox hitbox)
    {
        if (hitbox == null || hitbox.GetComponentInParent<Enemies.ProjectileScripts.Projectile>() != null) return;
        hitbox.ConfirmHit();
    }

    private static void ReportDodge(HitBox hitbox, bool isPerfect)
    {
        if (hitbox == null || hitbox.HasBeenParried) return;
        if (hitbox == lastDodgedHitbox && Time.time - lastDodgedTime < DodgeRepeatWindow) return;

        lastDodgedHitbox = hitbox;
        lastDodgedTime = Time.time;
        AnyAttackDodged?.Invoke(hitbox, isPerfect);
    }

    public bool TakeHazardHit(float damage, Vector2 contactPoint, float force, float staggerDuration, Vector2? directionOverride = null, bool dealsDamage = true, bool resetsPlayer = false, AnimationClip hurtClip = null, Color? hurtFlashColor = null)
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
        if (playerAnimation != null) playerAnimation.PlayHurtAnimation(hurtClip, hurtFlashColor);

        return true;
    }
}