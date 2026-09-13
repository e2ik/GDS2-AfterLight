using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    private Player player;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer sr;
    private Color ogColor;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash = Animator.StringToHash("yVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsWallSlidingHash = Animator.StringToHash("isWallSliding");
    private static readonly int IsDashingHash = Animator.StringToHash("isDashing");
    private static readonly int IsBouncingHash = Animator.StringToHash("isBouncing");
    private static readonly int IsDirectionalDashHash = Animator.StringToHash("isDirectionalDash");
    private static readonly int IsChargingSkillHash = Animator.StringToHash("isChargingSkill");
    private static readonly int IsParryingHash = Animator.StringToHash("isParrying");
    private static readonly int IsParrySuccessHash = Animator.StringToHash("isParrySuccess");
    private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");
    private static readonly int IsSkillingHash = Animator.StringToHash("isSkilling");
    private static readonly int IsPlungingHash = Animator.StringToHash("isPlunging");
    private static readonly int IsHurtHash = Animator.StringToHash("Hurt");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
    private static readonly int IsAboutToLandHash = Animator.StringToHash("isAboutToLand");
    private static readonly int ChargeProgressHash = Animator.StringToHash("ChargeProgress");
    private static readonly int IsDeadHash = Animator.StringToHash("isDead");

    [Header("particle prefabs")]
    [SerializeField] private ParticleSystem wallSlideParticleSystem;
    [SerializeField] private ParticleSystem skillChargeParticleSystem;

    private string lastPlayedSkill = string.Empty;
    private Coroutine flashColorCoroutine;
    private bool wasInvulnerable;
    private bool wasDeadLastFrame;

    private void Awake()
    {
        player = GetComponentInParent<Player>();
        animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponentInParent<Rigidbody2D>();
        if (sr == null)
        {
            sr = GetComponentInParent<SpriteRenderer>();
            if (sr != null) ogColor = sr.color;
        }

        if (wallSlideParticleSystem == null)
        {
            Transform wallSlideTransform = transform.Find("WallSlide");
            if (wallSlideTransform != null)
            {
                wallSlideParticleSystem = wallSlideTransform.GetComponent<ParticleSystem>();
            }
        }

        if (skillChargeParticleSystem == null)
        {
            Transform skillChargeTransform = transform.Find("SkillCharge");
            if (skillChargeTransform != null)
            {
                skillChargeParticleSystem = skillChargeTransform.GetComponent<ParticleSystem>();
            }
        }
    }

    private void LateUpdate()
    {
        if (player == null || rb == null) return;

        UpdateAnimationParameters();

        if (player.Stats.IsDead)
        {
            if (wallSlideParticleSystem != null)
                wallSlideParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (skillChargeParticleSystem != null)
                skillChargeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            return;
        }

        HandleSkillAnimation();
        HandleInvulnerabilityVisuals();
        HandleWallSlideVisuals();
        HandleSkillChargeVisuals();
    }

    private void UpdateAnimationParameters()
    {
        bool isDead = player.Stats.IsDead;
        animator.SetBool(IsDeadHash, isDead);

        if (isDead)
        {
            animator.SetFloat(SpeedHash, 0f);
            animator.SetFloat(YVelocityHash, 0f);
            animator.SetBool(IsGroundedHash, true);
            animator.SetBool(IsAboutToLandHash, false);

            animator.SetBool(IsWallSlidingHash, false);
            animator.SetBool(IsDashingHash, false);
            animator.SetBool(IsDirectionalDashHash, false);
            animator.SetBool(IsBouncingHash, false);

            animator.SetBool(IsChargingSkillHash, false);
            animator.SetFloat(ChargeProgressHash, 0f);

            animator.SetBool(IsParryingHash, false);
            animator.SetBool(IsParrySuccessHash, false);

            animator.SetBool(IsAttackingHash, false);
            animator.SetInteger(AttackIndexHash, 0);
            animator.SetBool(IsSkillingHash, false);
            animator.SetBool(IsPlungingHash, false);

            wasDeadLastFrame = true;
            return;
        }
        wasDeadLastFrame = false;

        bool isParrying = player.CombatController.IsParrying;
        bool aboutToLand = player.Controller.IsAboutToLand(out RaycastHit2D hit);
        animator.SetBool(IsAboutToLandHash, aboutToLand);

        animator.SetBool(IsChargingSkillHash, player.CombatController.IsChargeInputHeld);
        float maxDur = player.CombatController.ChargingSkillMaxDur;
        float chargeProgress = Mathf.Clamp01(player.CombatController.ChargingSkillTimer / maxDur);
        animator.SetFloat(ChargeProgressHash, chargeProgress);

        animator.SetBool(IsParryingHash, isParrying);
        animator.SetBool(IsParrySuccessHash, player.CombatController.IsParrySuccess);

        bool isSkilling = player.CombatController.IsSkilling;
        bool isPlunging = player.CombatController.IsPlunging;
        bool isBouncing = player.Controller.IsBouncing;

        bool isAttacking = (isParrying || isSkilling || isPlunging) ? false : player.CombatController.IsAttacking;
        int attackIndex = (isParrying || isSkilling || isPlunging) ? 0 : player.CombatController.CurrentComboIndex;

        animator.SetBool(IsAttackingHash, isAttacking);
        animator.SetInteger(AttackIndexHash, attackIndex);

        animator.SetBool(IsSkillingHash, isSkilling);
        animator.SetBool(IsPlungingHash, isPlunging);
        animator.SetBool(IsDashingHash, player.Controller.IsDashing);

        if (isSkilling) return;

        bool groundedForAnim = isAttacking
            ? player.CombatController.AttackStartedGrounded
            : player.Controller.IsGrounded;

        animator.SetFloat(SpeedHash, Mathf.Abs(rb.linearVelocityX));
        animator.SetFloat(YVelocityHash, rb.linearVelocityY);
        animator.SetBool(IsGroundedHash, groundedForAnim);
        animator.SetBool(IsWallSlidingHash, player.Controller.IsWallSliding);
        animator.SetBool(IsBouncingHash, isBouncing);
        animator.SetBool(IsDirectionalDashHash, player.Controller.IsDirectionalDash);
        animator.SetBool(IsChargingSkillHash, player.CombatController.IsChargeInputHeld);
    }

    private void HandleSkillAnimation()
    {
        if (player.CombatController.IsSkilling)
        {
            string currentGem = player.CombatController.CurrentSkillGemName;

            if (!string.IsNullOrEmpty(currentGem) && lastPlayedSkill != currentGem)
            {
                animator.Play(currentGem);
                lastPlayedSkill = currentGem;
            }
        }
        else
        {
            lastPlayedSkill = string.Empty;
        }
    }

    public void TriggerSpinSkillEffect()
    {
        PSpawner.Spawn("SwordAOE", transform.position, null, transform);
    }

    public void EndSkillAnimation()
    {
        player.CombatController.EndSkill();
    }

    #region Hit & Knockback Animation

    public void PlayHurtAnimation()
    {
        animator.SetTrigger(IsHurtHash);
        FlashRedOnHit();
        CamControls.Shake(0.1f, 0.5f);
    }

    public void FlashRedOnHit()
    {
        StartFlashColor(Color.red, 0.1f);
    }

    #endregion

    #region Parry Visuals

    public void FlashGreenOnParrySuccess()
    {
        StartFlashColor(Color.green, 0.15f);

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        Vector2 spawnPosition = transform.position;

        if (playerCollider != null)
        {
            Bounds bounds = playerCollider.bounds;
            Vector2 center = bounds.center;
            float randomX = UnityEngine.Random.Range(0.4f, 0.7f);
            float facingDir = player.Controller != null ? player.Controller.FacingDirection : 1f;
            float horizontalOffset = (bounds.extents.x + randomX) * facingDir;
            float randomY = UnityEngine.Random.Range(-bounds.extents.y + 0.7f, bounds.extents.y - 0.7f);

            spawnPosition = new Vector2(center.x + horizontalOffset, center.y + randomY);
        }

        PSpawner.Spawn("spark", spawnPosition);
        CamControls.Shake(0.15f, 0.1f);
    }

    #endregion

    public void TriggerJumpEffect(bool isWallJump, float wallDir = 0f)
    {
        if (player == null || player.Controller == null) return;

        Vector2 spawnPosition;
        Vector2 normal = player.Controller.CurrentSurfaceNormal;

        if (isWallJump) spawnPosition = player.Controller.LastHitPoint;
        else
        {
            Collider2D playerCollider = player.GetComponent<Collider2D>();

            if (playerCollider != null)
            {
                Bounds bounds = playerCollider.bounds;
                spawnPosition = new Vector2(bounds.center.x, bounds.min.y);
            }
            else
            {
                spawnPosition = Vector2.zero;
            }
        }

        if (spawnPosition == Vector2.zero)
        {
            spawnPosition = transform.position;
            normal = isWallJump ? new Vector2(-wallDir, 0f) : Vector2.up;
        }

        float angle = Mathf.Atan2(normal.y, -normal.x) * Mathf.Rad2Deg - 90f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        PSpawner.Spawn("JumpDust", spawnPosition, rotation);
    }

    public void TriggerDashEffect()
    {
        if (player == null || player.Controller == null) return;

        bool isDirectionalDash = player.Controller.IsDirectionalDash;

        float dashDirection = isDirectionalDash ? player.Controller.FacingDirection : -player.Controller.FacingDirection;

        Collider2D playerCollider = player.GetComponent<Collider2D>();

        Vector2 spawnPosition = transform.position;

        if (playerCollider != null)
        {
            Bounds bounds = playerCollider.bounds;

            float horizontalOffset = bounds.extents.x + 0.1f;

            spawnPosition = new Vector2( bounds.center.x - dashDirection * horizontalOffset, bounds.min.y);
        }

        ParticleSystem dashEffect = PSpawner.Spawn("DustDashEffect", spawnPosition);

        if (dashEffect != null)
        {
            Vector3 scale = dashEffect.transform.localScale;

            scale.x = dashDirection > 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);

            dashEffect.transform.localScale = scale;
        }
    }

    public void TriggerTurnDustEffect(int newFacingDirection)
    {
        if (player == null) return;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        Vector2 spawnPosition = transform.position;

        if (playerCollider != null)
        {
            Bounds bounds = playerCollider.bounds;
            float horizontalOffset = 0.5f;
            float offsetX = newFacingDirection > 0 ? horizontalOffset : -horizontalOffset;
            spawnPosition = new Vector2(bounds.center.x + offsetX, bounds.min.y);
        }

        ParticleSystem turnDust = PSpawner.Spawn("TurnDust", spawnPosition);

        if (turnDust != null)
        {
            Vector3 scale = turnDust.transform.localScale;
            scale.x = newFacingDirection > 0 ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
            turnDust.transform.localScale = scale;
        }
    }

    #region Helper Methods

    private void StartFlashColor(Color flashColor, float duration)
    {
        if (sr == null) return;
        if (flashColorCoroutine != null) StopCoroutine(flashColorCoroutine);
        flashColorCoroutine = StartCoroutine(FlashColorRoutine(flashColor, duration));
    }

    private IEnumerator FlashColorRoutine(Color flashColor, float duration)
    {
        sr.color = flashColor;
        yield return new WaitForSeconds(duration);
        sr.color = ogColor;
        flashColorCoroutine = null;
    }

    private void HandleInvulnerabilityVisuals()
    {
        if (sr == null || player == null || player.Controller == null) return;

        bool isInvuln = player.Controller.IsInvulnerable;

        if (flashColorCoroutine != null) return;

        if (isInvuln)
        {
            Color invulnColor = ogColor;
            invulnColor.a = 0.5f;
            sr.color = invulnColor;
            wasInvulnerable = true;
        }
        else if (wasInvulnerable)
        {
            sr.color = ogColor;
            wasInvulnerable = false;
        }
    }

    private void HandleWallSlideVisuals()
    {
        if (wallSlideParticleSystem == null || player.Controller == null) return;

        bool isWallSliding = player.Controller.IsWallSliding;

        if (isWallSliding)
        {
            if (!wallSlideParticleSystem.isEmitting)
            {
                wallSlideParticleSystem.Play();
            }
        }
        else
        {
            wallSlideParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void HandleSkillChargeVisuals()
    {
        if (skillChargeParticleSystem == null || player.CombatController == null) return;

        bool isCharging = player.CombatController.IsChargeInputHeld;

        if (isCharging)
        {
            if (!skillChargeParticleSystem.isEmitting)
            {
                skillChargeParticleSystem.Play();
            }

            float progress = Mathf.Clamp01(player.CombatController.ChargingSkillTimer / 1.5f);

            var main = skillChargeParticleSystem.main;
            main.startSize = Mathf.Lerp(0.1f, 0.25f, progress);
            main.simulationSpeed = Mathf.Lerp(1f, 4f, progress);
            main.startColor = Color.Lerp(Color.cyan, Color.yellow, progress);

            var emission = skillChargeParticleSystem.emission;
            emission.rateOverTime = Mathf.Lerp(1f, 25f, progress);
        }
        else
        {
            skillChargeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    #endregion
}