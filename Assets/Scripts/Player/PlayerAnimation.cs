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
    private static readonly int IsClimbingHash = Animator.StringToHash("isClimbing");
    private static readonly int IsHealingHash = Animator.StringToHash("isHealing");

    [Header("particle prefabs")]
    [SerializeField] private ParticleSystem wallSlideParticleSystem;
    [SerializeField] private ParticleSystem skillChargeParticleSystem;

    [Header("Skill Charge Glow")]
    [SerializeField] private Color chargeColorStart = Color.cyan;
    [SerializeField] private Color chargeColorEnd = Color.yellow;
    [SerializeField] private string chargeGlowProperty = "_Intensity";
    [SerializeField, Min(0f)] private float chargeGlowStart = 1f;
    [SerializeField, Min(0f)] private float chargeGlowEnd = 4f;

    private ParticleSystemRenderer skillChargeRenderer;
    private MaterialPropertyBlock chargeGlowBlock;
    private int chargeGlowPropertyId;
    private float chargeBaseAlpha = 1f;

    [Header("Plunge Land Effect")]
    [SerializeField] private string plungeLandEffect = "JumpDust";

    [Header("Healing Effect")]
    [SerializeField] private ParticleSystem healingParticleSystem;
    [SerializeField, Min(0f)] private float healingEffectDuration = 1f;
    [SerializeField] private bool ignoreRespawnRefill = true;

    private Coroutine healingEffectRoutine;
    private float lastHealth;
    private bool hasLastHealth;

    [Header("Parry Animation Sync")]
    [SerializeField] private string airParryState = "AirParry";
    [SerializeField] private string groundParryState = "GroundParry";

    private int airParryHash;
    private int groundParryHash;
    private bool wasGroundedForAnim;

    private string lastPlayedSkill = string.Empty;
    private Coroutine flashColorCoroutine;
    [SerializeField] private HitFlash hitFlash;

    [Header("Attack Glow")]
    [SerializeField] private MultiplyOutline attackGlowOutline;
    [SerializeField] private MultiplyOutline.GlowStyle[] comboGlows =
    {
        new MultiplyOutline.GlowStyle(),
        new MultiplyOutline.GlowStyle(),
        new MultiplyOutline.GlowStyle()
    };
    [SerializeField] private MultiplyOutline.GlowStyle skillGlow = new MultiplyOutline.GlowStyle();
    [SerializeField] private MultiplyOutline.GlowStyle plungeGlow = new MultiplyOutline.GlowStyle();
    [SerializeField] private MultiplyOutline.GlowStyle chargeGlow = new MultiplyOutline.GlowStyle();
    [SerializeField] private bool chargeGlowUsesChargeColor = true;
    [SerializeField] private bool chargeGlowRamps = true;
    [SerializeField, Range(0f, 10f)] private float chargeGlowFullIntensity = 4f;

    private readonly MultiplyOutline.GlowStyle chargeGlowRuntime = new MultiplyOutline.GlowStyle();
    [SerializeField, ColorUsage(false, true)] private Color hitFlashColor = new Color(2f, 0.3f, 0.3f, 1f);
    [SerializeField, ColorUsage(false, true)] private Color parryFlashColor = new Color(0.3f, 2f, 0.6f, 1f);
    [SerializeField, ColorUsage(false, true)] private Color healFlashColor = new Color(0.4f, 1.5f, 2f, 1f);
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

        chargeGlowBlock = new MaterialPropertyBlock();
        chargeGlowPropertyId = Shader.PropertyToID(chargeGlowProperty);

        airParryHash = Animator.StringToHash(airParryState);
        groundParryHash = Animator.StringToHash(groundParryState);

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

        if (healingParticleSystem == null && player != null)
        {
            foreach (ParticleSystem ps in player.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name == "HealingEffect")
                {
                    healingParticleSystem = ps;
                    break;
                }
            }
        }

        if (skillChargeParticleSystem != null)
        {
            skillChargeRenderer = skillChargeParticleSystem.GetComponent<ParticleSystemRenderer>();
            chargeBaseAlpha = skillChargeParticleSystem.main.startColor.color.a;
        }
    }

    private void Start()
    {
        if (player != null && player.Stats != null)
            player.Stats.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDestroy()
    {
        if (player != null && player.Stats != null)
            player.Stats.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (!hasLastHealth)
        {
            lastHealth = current;
            hasLastHealth = true;
            return;
        }

        bool gained = current > lastHealth;
        bool respawnRefill = ignoreRespawnRefill && lastHealth <= 0f;
        lastHealth = current;

        if (gained && !respawnRefill) PlayHealingEffect();
    }

    public void PlayHealingEffect()
    {
        if (healingParticleSystem == null) return;

        if (!healingParticleSystem.isEmitting) healingParticleSystem.Play(true);

        if (healingEffectRoutine != null) StopCoroutine(healingEffectRoutine);
        healingEffectRoutine = StartCoroutine(StopHealingEffectAfter(healingEffectDuration));
    }

    private IEnumerator StopHealingEffectAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        StopHealingEffect();
    }

    private void StopHealingEffect()
    {
        if (healingEffectRoutine != null)
        {
            StopCoroutine(healingEffectRoutine);
            healingEffectRoutine = null;
        }

        if (healingParticleSystem != null)
            healingParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void LateUpdate()
    {
        if (player == null || rb == null) return;

        UpdateAnimationParameters();

        if (player.Stats.IsDead)
        {
            SetAttackGlow(null);
            if (wallSlideParticleSystem != null)
                wallSlideParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (skillChargeParticleSystem != null)
                skillChargeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (healingEffectRoutine != null) StopHealingEffect();
            return;
        }

        HandleSkillAnimation();
        HandleInvulnerabilityVisuals();
        HandleWallSlideVisuals();
        HandleSkillChargeVisuals();
        HandleAttackGlow();
    }

    private void HandleAttackGlow()
    {
        if (player.CombatController == null) return;

        PlayerCombatController combat = player.CombatController;
        MultiplyOutline.GlowStyle style = null;

        if (combat.IsPlunging) style = plungeGlow;
        else if (combat.IsSkilling) style = skillGlow;
        else if (combat.IsChargeInputHeld) style = ChargeGlow(combat);
        else if (combat.IsAttacking) style = ComboGlow(combat.CurrentComboIndex);

        SetAttackGlow(style);
    }

    private MultiplyOutline.GlowStyle ChargeGlow(PlayerCombatController combat)
    {
        if (chargeGlow == null) return null;

        float maxDur = Mathf.Max(0.01f, combat.ChargingSkillMaxDur);
        float progress = Mathf.Clamp01(combat.ChargingSkillTimer / maxDur);

        Color color = chargeGlow.color;
        if (chargeGlowUsesChargeColor)
        {
            color = Color.Lerp(chargeColorStart, chargeColorEnd, progress);
            color.a = 1f;
        }

        chargeGlowRuntime.enabled = chargeGlow.enabled;
        chargeGlowRuntime.color = color;
        chargeGlowRuntime.intensity = chargeGlowRamps
            ? Mathf.Lerp(chargeGlow.intensity, chargeGlowFullIntensity, progress)
            : chargeGlow.intensity;
        chargeGlowRuntime.checkerLow = chargeGlow.checkerLow;
        chargeGlowRuntime.checkerSize = chargeGlow.checkerSize;
        return chargeGlowRuntime;
    }

    private MultiplyOutline.GlowStyle ComboGlow(int comboIndex)
    {
        if (comboGlows == null || comboGlows.Length == 0) return null;
        return comboGlows[Mathf.Clamp(comboIndex - 1, 0, comboGlows.Length - 1)];
    }

    private void SetAttackGlow(MultiplyOutline.GlowStyle style)
    {
        if (attackGlowOutline == null)
        {
            if (sr != null) attackGlowOutline = sr.GetComponent<MultiplyOutline>();
            if (attackGlowOutline == null) attackGlowOutline = GetComponentInParent<MultiplyOutline>();
            if (attackGlowOutline == null) return;
        }

        attackGlowOutline.SetColorGlow(style);
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
            
            animator.SetBool(IsClimbingHash, false);
            animator.SetBool(IsHealingHash, false);

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
        animator.SetBool(IsClimbingHash, player.Controller.IsClimbing);
        animator.SetBool(IsHealingHash, player.Controller.IsHealing);

        if (isSkilling) return;

        bool groundedForAnim = isAttacking
            ? player.CombatController.AttackStartedGrounded
            : player.Controller.IsGrounded;

        SyncParryAcrossGroundChange(isParrying, groundedForAnim);

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
        CamControls.Shake(0.1f, 0.8f);
    }

    public void FlashRedOnHit()
    {
        if (ResolveHitFlash() != null)
        {
            hitFlash.Flash(hitFlashColor);
            return;
        }

        StartFlashColor(Color.red, 0.1f);
    }

    public void FlashOnHeal()
    {
        if (ResolveHitFlash() != null) hitFlash.Flash(healFlashColor);
    }

    private HitFlash ResolveHitFlash()
    {
        if (hitFlash == null && sr != null) hitFlash = sr.GetComponent<HitFlash>();
        return hitFlash;
    }

    #endregion

    #region Parry Visuals

    public void FlashGreenOnParrySuccess()
    {
        if (ResolveHitFlash() != null) hitFlash.Flash(parryFlashColor);
        else StartFlashColor(Color.green, 0.15f);

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
        CamControls.Shake(0.15f, 0.3f);
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

    public void TriggerJumpEffectAt(Vector2 position, Vector2 normal)
    {
        float angle = Mathf.Atan2(normal.y, -normal.x) * Mathf.Rad2Deg - 90f;
        PSpawner.Spawn("JumpDust", position, Quaternion.Euler(0f, 0f, angle));
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

    public void TriggerLandingEffect()
    {
        if (player == null || player.Controller == null) return;

        Vector2 groundPoint = player.Controller.LastHitPoint;
        Collider2D playerCollider = player.GetComponent<Collider2D>();

        Vector2 spawnPosition = new Vector2(playerCollider.bounds.center.x,groundPoint.y);
        Vector2 normal = player.Controller.CurrentSurfaceNormal;

        float angle = Mathf.Atan2(normal.y, -normal.x) * Mathf.Rad2Deg - 90f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        PSpawner.Spawn("JumpDust", spawnPosition, rotation);
    }

    public void TriggerPlungeLandEffect()
    {
        if (player == null || player.Controller == null || string.IsNullOrEmpty(plungeLandEffect)) return;

        Collider2D playerCollider = player.GetComponent<Collider2D>();
        Vector2 groundPoint = player.Controller.LastHitPoint;
        float x = playerCollider != null ? playerCollider.bounds.center.x : transform.position.x;
        Vector2 normal = player.Controller.CurrentSurfaceNormal;

        float angle = Mathf.Atan2(normal.y, -normal.x) * Mathf.Rad2Deg - 90f;
        PSpawner.Spawn(plungeLandEffect, new Vector2(x, groundPoint.y), Quaternion.Euler(0f, 0f, angle));
    }

    #region Helper Methods

    private void SyncParryAcrossGroundChange(bool isParrying, bool groundedNow)
    {
        if (isParrying && groundedNow != wasGroundedForAnim)
        {
            AnimatorStateInfo info = animator.IsInTransition(0)
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);

            int from = wasGroundedForAnim ? groundParryHash : airParryHash;
            int to = groundedNow ? groundParryHash : airParryHash;

            if (info.shortNameHash == from || info.fullPathHash == from)
                animator.Play(to, 0, info.normalizedTime);
        }

        wasGroundedForAnim = groundedNow;
    }

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

        bool isInvuln = player.Controller.IsNeutralDashInvulnerable;

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

            float maxDur = Mathf.Max(0.01f, player.CombatController.ChargingSkillMaxDur);
            float progress = Mathf.Clamp01(player.CombatController.ChargingSkillTimer / maxDur);

            var main = skillChargeParticleSystem.main;
            main.simulationSpeed = Mathf.Lerp(1f, 4f, progress);
            Color chargeColor = Color.Lerp(chargeColorStart, chargeColorEnd, progress);
            chargeColor.a = chargeBaseAlpha;
            main.startColor = chargeColor;

            var emission = skillChargeParticleSystem.emission;
            emission.rateOverTime = Mathf.Lerp(1f, 25f, progress);

            SetChargeGlow(Mathf.Lerp(chargeGlowStart, chargeGlowEnd, progress));
        }
        else
        {
            skillChargeParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SetChargeGlow(float intensity)
    {
        if (skillChargeRenderer == null) return;

        skillChargeRenderer.GetPropertyBlock(chargeGlowBlock);
        chargeGlowBlock.SetFloat(chargeGlowPropertyId, intensity);
        skillChargeRenderer.SetPropertyBlock(chargeGlowBlock);
    }

    #endregion
}