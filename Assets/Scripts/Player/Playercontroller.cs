using System.Collections;
using System.Collections.Generic;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Player))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float tapJumpMultiplier = 0.2f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 8f;
    [SerializeField] private float velocityPower = 1.2f;
    [SerializeField] private float friction = 0.2f;
    [SerializeField] private float passThroughPlatformDuration = 0.25f;

    [SerializeField] private float verticalDeadzone = 0.3f;
    [SerializeField] private float upIntentThreshold = 0.5f;
    [SerializeField] private float downIntentThreshold = 0.7f;
    public float VerticalInput => verticalInput;
    public bool IsUpIntent { get; private set; }
    public bool IsDownIntent { get; private set; }

    [Header("Gravity Settings")]
    [SerializeField] private float normGravity = 3f;
    [SerializeField] private float jumpGravity = 2.5f;
    [SerializeField] private float fallGravity = 4.5f;
    [SerializeField] private float plungeGravity = 10f;
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("Wall Settings")]
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private float wallSlideGracePeriod = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float wallJumpCounterStrength = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float wallSlideUpwardDampening = 0.5f;
    [SerializeField] private float wallCheckNormalThreshold = 0.5f;
    [SerializeField] private Vector2 wallJumpForce = new(8f, 16f);
    [SerializeField] private float wallJumpDuration = 0.4f;
    [SerializeField] private float wallJumpBufferTime = 0.2f;

    [Header("Step Up Settings")]
    [SerializeField] private float maxStepHeight = 0.3f;
    [SerializeField] private float stepCheckDistance = 0.15f;
    [SerializeField] private float stepSmoothSpeed = 6f;

    [Header("Dash Settings")]
    [SerializeField] private float dashVelocity = 20f;
    [SerializeField] [Range(0.1f, 1f)] private float backDashMultiplier = 0.5f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCoolDown = 0.2f;
    [SerializeField] private float dashSkillEnergyCost = 0.1f;
    [SerializeField] private float neutralDashInvulnExtension = 0.1f;
    private float neutralDashInvulnTimer;

    [Header("Knockback Settings")]
    [SerializeField] private bool enemyBodyCollisionKnockback = true;
    [SerializeField] private AttackForce enemyBodyCollisionForce = AttackForce.Light;
    [SerializeField] private float hazardousKnockbackForce = 12f;
    [SerializeField] private float hazardousStaggerDuration = 0.3f;
    [SerializeField] private float bounceDuration = 0.2f;
    public float BounceDuration => bounceDuration;
    [SerializeField] private float lightForce = 8f, lightStaggerDuration = 0.1f;
    [SerializeField] private float mediumForce = 10f, mediumStaggerDuration = 0.2f;
    [SerializeField] private float heavyForce = 14f, heavyStaggerDuration = 0.4f;


    [Header("Edge Climb Settings")]
    [Tooltip("Where the player is placed when the climb starts, relative to the ledge corner (x is mirrored by facing).")]
    [SerializeField] private Vector2 climbStartOffset;
    [Tooltip("Where the player ends up when the climb finishes, relative to the ledge corner (x is mirrored by facing).")]
    [SerializeField] private Vector2 climbEndOffset;
    [Tooltip("Safety net: finishes the climb if the Climb animation event hasn't fired after this long (e.g. the animation got interrupted). Set it a little above the climb clip's length.")]
    [SerializeField] private float climbTimeout = 1.5f;
    [Tooltip("Delay after a climb ends or is cancelled before another climb can start.")]
    [SerializeField] private float climbCooldown = 0.1f;

    private PlayerEdgeDetection edgeDetection;
    private Vector2 climbStartPos;
    private Vector2 climbEndPos;
    private float climbStartTime;
    private bool canClimbEdge = true;
    private bool isClimbing;

    // True while a climbable ledge is beside the player. Set by the controller each physics
    // step; other scripts can read it but shouldn't write it.
    [HideInInspector] public bool onEdge;

    [Header("Detection Settings")]
    public LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private float groundCheckNormalThreshold = 0.6f;
    [SerializeField] private float wallCheckDistance = 0.05f;
    [SerializeField] private float edgeMargin = 0.05f;

    private bool jumpPressed, jumpReleased, isGrounded, onWall, isWallSliding, isWallJumping;
    private bool dashPressed, dashReleased, isDashing, isStaggered, isBouncing;
    private bool isChargingSkillPhysics, isSkillGravityZeroed, isParryGravityActive, inventoryPressed;
    private bool healPressed, isHealing;
    private const float InputDeadzone = 0.1f;
    private Collider2D currentPassThroughPlatform;

    private float horizontalInput, verticalInput, coyoteTimeCounter, wallCoyoteTimer, wallJumpTimer, wallContactTimer;
    private float dashTimer, dashDirection, wallJumpDirection;
    private bool wasWallSliding;
    private int movementFreezeCount;

    private PlayerAnimation playerAnimation;
    private PlayerCombatController combat;
    private PlayerHeals heals;
    private Rigidbody2D rb;
    private Collider2D[] playerColliders;
    private Collider2D[] boundsColliders;
    private InventoryDisplay inventoryDisplay;
    private Coroutine hitStaggerRoutine;
    private Coroutine bounceRoutine;
    private Bounds cachedBounds;
    private Vector2 LastGroundedPos { get; set; }

    public bool InputEnabled { get; set; } = true;
    public int FacingDirection { get; private set; } = 1;
    public bool IsMovementFrozen => movementFreezeCount > 0;
    public bool IsUILocked =>
        GameUI.UIManager.Instance != null && GameUI.UIManager.Instance.IsInputLocked;
    public bool IsGrounded => isGrounded;
    public bool IsWallSliding => isWallSliding;
    public bool IsDashing => isDashing;
    private bool isDashLocked;
    public bool IsDashLocked => isDashLocked;
    public bool IsChargingSkill => isChargingSkillPhysics;
    public bool IsDirectionalDash { get; private set; }
    public bool IsStaggered => isStaggered;
    public bool IsBouncing => isBouncing;
    public bool IsNeutralDash => isDashing && !IsDirectionalDash;
    public bool IsInvulnerable => IsNeutralDash || neutralDashInvulnTimer > 0f;
    public bool IsClimbing => isClimbing;
    public bool IsHealing => isHealing;
    private bool IsSkillBaseLocked =>
        isChargingSkillPhysics
        || isSkillGravityZeroed
        || combat.IsChargeInputHeld;

    public bool IsSkillActive => IsSkillBaseLocked || combat.IsSkilling;
    public bool IsMovementLockedBySkill => IsSkillBaseLocked || combat.IsSkillingWithMovementLock;
    private bool IsFrozenOrSkillLocked => IsMovementFrozen || IsMovementLockedBySkill || combat.IsPlunging;

    private Vector2 currentSurfaceNormal = Vector2.up;
    public Vector2 CurrentSurfaceNormal => currentSurfaceNormal;
    private Vector2 lastHitPoint;
    public Vector2 LastHitPoint => lastHitPoint;
    private bool physicsSuspended;
    private float preSuspendGravityScale;
    public bool IsPhysicsSuspended => physicsSuspended;

    private int lastFacingDirection;

    private void Awake()
    {
        Player player = GetComponent<Player>();
        playerAnimation = player.Animation;
        combat = player.CombatController;
        heals = player.Heals;
        rb = GetComponent<Rigidbody2D>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        boundsColliders = BuildBoundsColliders();
        edgeDetection = GetComponentInChildren<PlayerEdgeDetection>(true);
    }

    private void Start()
    {
        rb.gravityScale = normGravity;
        lastFacingDirection = FacingDirection;
    }

    private void Update()
    {
        if (neutralDashInvulnTimer > 0f) neutralDashInvulnTimer -= Time.deltaTime;

        if (!IsUILocked && CanMove()) Flip();
        if (InputEnabled && !IsMovementFrozen)
        {
            PerformInventoryAction(); 
            HandleHeal();
        }
    }

    private void FixedUpdate()
    {
        if (physicsSuspended) return;

        cachedBounds = ComputePlayerBounds();

        GroundCheckUpdate();
        WallCheckUpdate();

        HandleEdgeClimb();
        HandleStepUp();
        HandleMovement();
        HandleWallSlide();
        HandleJump();
        HandleWallJump();
        HandleDash();
        HandlePlunge();

        UpdateGravity();
    }

    public void FreezeMovement(bool freeze)
    {
        movementFreezeCount = freeze ? movementFreezeCount + 1 : Mathf.Max(0, movementFreezeCount - 1);
        if (freeze) rb.linearVelocity = new Vector2(0f, rb.linearVelocityY);
    }

    public void SetPhysicsSuspended(bool suspend)
    {
        if (suspend == physicsSuspended) return;
        physicsSuspended = suspend;

        SetCollidersEnabled(!suspend);

        if (suspend)
        {
            CancelClimb();
            preSuspendGravityScale = rb.gravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
        }
        else
        {
            rb.gravityScale = preSuspendGravityScale;
        }
    }

    public void SetCollidersEnabled(bool enabledState)
    {
        if (playerColliders == null || playerColliders.Length == 0)
            playerColliders = GetComponentsInChildren<Collider2D>(true);

        foreach (var col in playerColliders)
        {
            if (col != null) col.enabled = enabledState;
        }
    }

    private bool IsGravityZeroed => isParryGravityActive || isChargingSkillPhysics || isSkillGravityZeroed || isClimbing;

    private void ApplyGravityZeroLock()
    {
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
    }

    public void SetParryGravity(bool active)
    {
        isParryGravityActive = active;
        if (active) ApplyGravityZeroLock();
        else UpdateGravity();
    }

    public void SetSkillCharging(bool active)
    {
        isChargingSkillPhysics = active;
        if (active)
        {
            ApplyGravityZeroLock();
            isWallSliding = false;
        }
        else
        {
            UpdateGravity();
        }
    }

    public void SetSkillGravityZero(bool active)
    {
        isSkillGravityZeroed = active;
        if (active) ApplyGravityZeroLock();
        else UpdateGravity();
    }

    public bool CanMove() => InputEnabled
                             && !IsMovementFrozen
                             && !isWallJumping
                             && !isDashing
                             && !isStaggered
                             && !isWallSliding
                             && !IsMovementLockedBySkill
                             && !combat.IsPlunging
                             && !isClimbing
                             && !isHealing;

    #region Movement Handlers

    private void HandleMovement()
    {
        if (IsMovementFrozen || IsMovementLockedBySkill || isStaggered || isClimbing) return;

        bool duringWallJump = isWallJumping && Mathf.Abs(horizontalInput) > InputDeadzone;
        if (!CanMove() && !duringWallJump) return;

        float targetSpeed = horizontalInput * moveSpeed;
        float speedDif = targetSpeed - rb.linearVelocityX;
        float accelRate = (Mathf.Abs(horizontalInput) > InputDeadzone) ? acceleration : deceleration;
        float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, velocityPower) * Mathf.Sign(speedDif);

        if (isWallJumping && horizontalInput != 0f && Mathf.Sign(horizontalInput) != Mathf.Sign(wallJumpDirection))
            movement *= wallJumpCounterStrength;

        rb.AddForce(movement * Vector2.right);

        if (isGrounded && Mathf.Abs(horizontalInput) < InputDeadzone)
        {
            float f = Mathf.Min(Mathf.Abs(rb.linearVelocityX), friction) * Mathf.Sign(rb.linearVelocityX);
            rb.AddForce(Vector2.right * -f, ForceMode2D.Impulse);
        }
    }

    private void HandleStepUp()
    {
        if (!isGrounded) return;
        if (IsMovementFrozen || IsMovementLockedBySkill || isStaggered || isWallSliding || isDashing || isClimbing) return;
        if (Mathf.Abs(horizontalInput) < InputDeadzone) return;

        Bounds bounds = cachedBounds;
        float dir = Mathf.Sign(horizontalInput);

        Vector2 lowOrigin = new(bounds.center.x + dir * bounds.extents.x, bounds.min.y + 0.05f);
        RaycastHit2D lowHit = Physics2D.Raycast(lowOrigin, Vector2.right * dir, stepCheckDistance, groundLayer);
        if (lowHit.collider == null) return;

        Vector2 highOrigin = new(lowOrigin.x, bounds.min.y + maxStepHeight);
        RaycastHit2D highHit = Physics2D.Raycast(highOrigin, Vector2.right * dir, stepCheckDistance, groundLayer);
        if (highHit.collider != null) return;

        rb.position += new Vector2(0f, stepSmoothSpeed * Time.fixedDeltaTime);
    }

    private void HandleJump()
    {
        bool canJump = InputEnabled
                       && !isWallJumping
                       && !isStaggered
                       && !isWallSliding
                       && !IsMovementLockedBySkill
                       && !combat.IsPlunging
                       && !isClimbing
                       && !isHealing;
        if (!canJump) return;

        coyoteTimeCounter = isGrounded ? coyoteTime : coyoteTimeCounter - Time.fixedDeltaTime;

        if (jumpPressed && coyoteTimeCounter > 0f)
        {
            if (verticalInput < -0.5f && TryPassThroughPlatform())
            {
                ConsumeJumpInput();
                return;
            }

            if (isDashing)
            {
                CancelInvoke(nameof(StopDashing));
                StopDashing();
            }

            combat.ForceCancelAttack();
            combat.NotifyJumpInputReceived();

            if (!isGrounded) rb.linearVelocityY = 0f;
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            playerAnimation.TriggerJumpEffect(false);

            ConsumeJumpInput();
            coyoteTimeCounter = 0f;
        }

        if (jumpReleased)
        {
            if (rb.linearVelocityY > 0)
                rb.AddForce(Vector2.down * (rb.linearVelocityY * (1f - tapJumpMultiplier)), ForceMode2D.Impulse);
            jumpReleased = false;
        }
    }

    private void HandleWallSlide()
    {
        if (IsFrozenOrSkillLocked || combat.IsAttacking || isClimbing)
        {
            isWallSliding = false;
            wallContactTimer = 0f;
            return;
        }

        wallCoyoteTimer = (onWall && !isGrounded && Mathf.Abs(horizontalInput) > InputDeadzone) ? coyoteTime : wallCoyoteTimer - Time.fixedDeltaTime;

        bool touchingWall = onWall && !isGrounded && wallCoyoteTimer > 0f;
        wallContactTimer = touchingWall ? wallContactTimer + Time.fixedDeltaTime : 0f;

        if (touchingWall && wallContactTimer >= wallSlideGracePeriod)
        {
            if (!isWallSliding)
            {
                combat.ForceCancelAttack();
                combat.CancelParry();
                if (combat.IsSkilling) combat.EndSkill();
            }

            isWallSliding = true;
            rb.linearVelocityX = 0f;
            if (rb.linearVelocityY > 0f) rb.linearVelocityY *= wallSlideUpwardDampening;
            rb.linearVelocityY = Mathf.Clamp(rb.linearVelocityY, -wallSlideSpeed, float.MaxValue);
        }
        else
        {
            isWallSliding = false;
        }
    }

    private void HandleWallJump()
    {
        if (IsFrozenOrSkillLocked || isClimbing) return;

        if (isWallSliding)
        {
            isWallJumping = false;
            if (!wasWallSliding) wallJumpDirection = -FacingDirection;
            wallJumpTimer = wallJumpBufferTime;
            CancelInvoke(nameof(StopWallJumping));
        }
        else
        {
            wallJumpTimer -= Time.fixedDeltaTime;
        }

        wasWallSliding = isWallSliding;

        if (jumpPressed && wallJumpTimer > 0f)
        {
            isWallJumping = true;
            combat.ForceCancelAttack();
            combat.NotifyJumpInputReceived();
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(new Vector2(wallJumpDirection * wallJumpForce.x, wallJumpForce.y), ForceMode2D.Impulse);
            currentSurfaceNormal = new Vector2(-wallJumpDirection, 0f);

            playerAnimation.TriggerJumpEffect(true, wallJumpDirection);

            wallJumpTimer = 0f;
            ConsumeJumpInput();

            if (FacingDirection != wallJumpDirection)
            {
                FacingDirection = (int)wallJumpDirection;
                transform.localScale = new Vector3(FacingDirection, transform.localScale.y, transform.localScale.z);
            }
            Invoke(nameof(StopWallJumping), wallJumpDuration);
        }
    }

    private void StopWallJumping() => isWallJumping = false;

    private void HandleDash()
    {
        dashTimer = isDashing ? dashCoolDown : dashTimer - Time.fixedDeltaTime;

        if (dashPressed && isGrounded && dashTimer <= 0f)
        {
            if (isClimbing) return;
            if (combat.IsPlunging) return;
            if (isBouncing) return;
            if (combat.IsChargeInputHeld) return;
            if (combat.IsSkilling) return;
            if (combat.SkillMeter <= 0f) return;
            if (combat.IsParrying) combat.CancelParry();
            if (IsMovementFrozen) return;
            if (combat.IsAttacking) combat.ForceCancelAttack();

            combat.ChargeSkillMeter(-dashSkillEnergyCost);

            isDashing = true;
            isDashLocked = false;

            combat.NotifyDashInputReceived();

            float activeDuration = dashDuration;

            if (Mathf.Abs(horizontalInput) > InputDeadzone)
            {
                IsDirectionalDash = true;
                dashDirection = Mathf.Sign(horizontalInput);

                if (FacingDirection != dashDirection)
                {
                    FacingDirection = (int)dashDirection;
                    transform.localScale = new Vector3(FacingDirection, transform.localScale.y, transform.localScale.z);
                }
            }
            else
            {
                IsDirectionalDash = false;
                dashDirection = -FacingDirection;
                activeDuration *= backDashMultiplier;
            }

            IgnoreAllAirOnlyPlatformsDuringDash(activeDuration);

            rb.linearVelocity = new Vector2(dashDirection * dashVelocity, rb.linearVelocity.y);

            playerAnimation.TriggerDashEffect();

            ConsumeDashInput();

            CancelInvoke(nameof(StopDashing));
            Invoke(nameof(StopDashing), activeDuration);
        }

        if (dashReleased) ConsumeDashInput();
    }

    private void IgnoreAllAirOnlyPlatformsDuringDash(float duration)
    {
        AirOnlyCollisionPlatform[] platforms = FindObjectsByType<AirOnlyCollisionPlatform>(FindObjectsSortMode.None);
        foreach (var platform in platforms)
        {
            platform.IgnoreCollisionFor(playerColliders, duration);
        }
    }

    public void SetDashLockedDuringAttack(bool locked)
    {
        if (locked)
        {
            CancelInvoke(nameof(StopDashing));
            isDashing = true;
            isDashLocked = true;
        }
        else
        {
            StopDashing();
        }
    }

    public void CancelDash()
    {
        CancelInvoke(nameof(StopDashing));
        StopDashing();
    }

    private void StopDashing()
    {
        if (isDashing && !IsDirectionalDash && !isDashLocked)
            neutralDashInvulnTimer = neutralDashInvulnExtension;

        isDashing = false;
        isDashLocked = false;
    }

    private void HandlePlunge()
    {
        if (!combat.IsPlunging || isBouncing || isStaggered || isClimbing) return;
        if (rb.linearVelocityY > 0.1f) rb.linearVelocityY = 0f;
        rb.linearVelocityX = 0;
    }

    private void HandleEdgeClimb()
    {
        if (isClimbing)
        {
            rb.linearVelocity = Vector2.zero;
            if (Time.time - climbStartTime >= climbTimeout) Climb();
            return;
        }

        onEdge = false;
        if (edgeDetection == null || !canClimbEdge) return;
        if (!InputEnabled || IsFrozenOrSkillLocked || isStaggered || isBouncing || isDashing) return;

        // Look for a ledge on the side being pressed toward, not the side being faced.
        // Turning is locked during a wall jump, so using facing made ledges on the other
        // side undetectable until the wall jump ended.
        bool pressing = Mathf.Abs(horizontalInput) > InputDeadzone;
        int dir = pressing ? (horizontalInput > 0f ? 1 : -1) : FacingDirection;

        if (!edgeDetection.TryFindLedge(cachedBounds, dir, out Vector2 corner)) return;
        onEdge = true;

        // Only climb while holding toward the ledge.
        if (pressing) StartClimb(corner, dir);
    }

    private void StartClimb(Vector2 corner, int dir)
    {
        canClimbEdge = false;
        isClimbing = true;
        climbStartTime = Time.time;

        combat.CancelAllActions();

        // The climb takes over from a wall jump or wall slide.
        isWallJumping = false;
        isWallSliding = false;
        CancelInvoke(nameof(StopWallJumping));

        if (FacingDirection != dir)
        {
            FacingDirection = dir;
            transform.localScale = new Vector3(FacingDirection, transform.localScale.y, transform.localScale.z);
        }

        climbStartPos = corner + new Vector2(climbStartOffset.x * dir, climbStartOffset.y);
        climbEndPos = corner + new Vector2(climbEndOffset.x * dir, climbEndOffset.y);

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        TeleportTo(climbStartPos);
    }

    private void Climb() //triggered in animation events
    {
        // Ignore a late event from a climb that was already cancelled or finished.
        if (!isClimbing) return;

        isClimbing = false;
        TeleportTo(climbEndPos);
        rb.linearVelocity = Vector2.zero;
        Invoke(nameof(AllowClimb), climbCooldown);
    }

    // Stops a climb part-way (knockback, bounce, physics suspended) without moving the player.
    public void CancelClimb()
    {
        if (!isClimbing) return;

        isClimbing = false;
        Invoke(nameof(AllowClimb), climbCooldown);
    }

    private void AllowClimb() => canClimbEdge = true;

    private void TeleportTo(Vector2 position)
    {
        rb.position = position;
        transform.position = position;
    }

    private void UpdateGravity()
    {
        if (IsGravityZeroed)
        {
            rb.gravityScale = 0f;
        }
        else if (combat.IsPlunging && !isBouncing)
            rb.gravityScale = plungeGravity;
        else
            rb.gravityScale = rb.linearVelocityY switch
            {
                > 0.1f => jumpGravity,
                < -0.1f => fallGravity,
                _ => normGravity
            };
    }

    public void ResetPosition()
    {
        if(FadeCanvasController.Instance != null)
            StartCoroutine(ResetPosCoroutine());
        else
            transform.position = LastGroundedPos;
    }

    [SerializeField] private float fadeDuration = 0.1f;
    private IEnumerator ResetPosCoroutine()
    {
        FreezeMovement(true);
        FadeCanvasController.Instance.FadeOut(fadeDuration);
        yield return new WaitForSeconds(fadeDuration * 4);
        transform.position = LastGroundedPos;
        FreezeMovement(false);
        yield return new WaitForSeconds(fadeDuration);
        FadeCanvasController.Instance.FadeIn(fadeDuration);
    }

    private Coroutine attemptHealRoutine;
    [SerializeField] private float healDuration = 0.1f;
    private void HandleHeal()
    {
        if (!CanMove() || !isGrounded || !healPressed || isHealing)
        {
            healPressed = false;
            return;
        }
        healPressed = false;
        isHealing = true;
        rb.linearVelocityX = 0;
    }

    public void CancelHeal()
    {
        isHealing = false;
    }
    public void FinishHealing() //animation event
    {
        if (!isHealing) return;
        heals.TryUseHeal();
        isHealing = false;
    }

    #endregion

    #region Damage & Stagger

    private readonly struct KnockbackData
    {
        public readonly float Force;
        public readonly float StaggerDuration;

        public KnockbackData(float force, float staggerDuration)
        {
            Force = force;
            StaggerDuration = staggerDuration;
        }
    }

    public void ApplyKnockback(Vector2 sourcePosition, AttackForce attackForce, bool applyStagger = true, bool playHurtAnimation = true)
    {
        if (physicsSuspended) return;

        CancelClimb();
        combat.ForceCancelAttack();
        if (applyStagger && playHurtAnimation) playerAnimation.PlayHurtAnimation();

        KnockbackData data = attackForce switch
        {
            AttackForce.Light => new KnockbackData(lightForce, lightStaggerDuration),
            AttackForce.Medium => new KnockbackData(mediumForce, mediumStaggerDuration),
            AttackForce.Heavy => new KnockbackData(heavyForce, heavyStaggerDuration),
            _ => new KnockbackData(0f, 0f)
        };

        Vector2 dir = ((Vector2)transform.position - sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * data.Force, ForceMode2D.Impulse);

        if (applyStagger) StartHitStagger(data.StaggerDuration);
    }

    private void StartHitStagger(float duration)
    {
        if (hitStaggerRoutine != null) StopCoroutine(hitStaggerRoutine);
        hitStaggerRoutine = StartCoroutine(HitStaggerCoroutine(duration));
    }

    private IEnumerator HitStaggerCoroutine(float duration)
    {
        isStaggered = true;
        yield return new WaitForSeconds(duration);
        isStaggered = false;
        hitStaggerRoutine = null;
    }

    public void ApplyBounceImpulse(Vector2 sourcePosition, float force)
    {
        if (physicsSuspended) return;

        CancelClimb();
        Vector2 dir = ((Vector2)transform.position - sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * force, ForceMode2D.Impulse);
    }

    public void TriggerBounce(Vector2 sourcePosition, float force, float duration)
    {
        if (physicsSuspended) return;

        CancelClimb();
        combat.ForceCancelAttack();

        Vector2 dir = ((Vector2)transform.position - sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * force, ForceMode2D.Impulse);

        PlayBounceState(duration);
    }

    public void PlayBounceState(float duration)
    {
        if (physicsSuspended) return;

        if (bounceRoutine != null) StopCoroutine(bounceRoutine);
        bounceRoutine = StartCoroutine(BounceCoroutine(duration));
    }

    private IEnumerator BounceCoroutine(float duration)
    {
        isBouncing = true;
        yield return new WaitForSeconds(duration);
        isBouncing = false;
        bounceRoutine = null;
    }

    #endregion

    #region Input Callbacks

    public void OnMove(InputValue value)
    {
        Vector2 raw = value.Get<Vector2>();

        verticalInput = Mathf.Abs(raw.y) > verticalDeadzone ? raw.y : 0f;

        bool verticalDominant = Mathf.Abs(raw.y) > Mathf.Abs(raw.x);
        IsUpIntent = raw.y > upIntentThreshold && verticalDominant;
        IsDownIntent = raw.y < -downIntentThreshold && verticalDominant;

        horizontalInput = Mathf.Abs(raw.x) > InputDeadzone
            ? Mathf.Sign(raw.x) * Mathf.Clamp01(raw.magnitude)
            : 0f;
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed && IsUILocked) return;

        if (value.isPressed)
        {
            combat.CancelParry();
        }
        jumpPressed = value.isPressed; jumpReleased = !value.isPressed;
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed && IsUILocked) return;

        dashPressed = value.isPressed; dashReleased = !value.isPressed;
    }

    public void OnInventory() => inventoryPressed = true;

    private void ConsumeJumpInput()
    {
        jumpPressed = false;
        jumpReleased = false;
    }

    private void ConsumeDashInput()
    {
        dashPressed = false;
        dashReleased = false;
    }
    
    private void OnHeal() => healPressed = true;

    #endregion

    #region Physics Checks & Utilities

    private void Flip()
    {
        if (Mathf.Abs(horizontalInput) > InputDeadzone)
        {
            int newFacingDirection = horizontalInput > 0f ? 1 : -1;

            if (newFacingDirection != FacingDirection)
            {
                FacingDirection = newFacingDirection;

                if (isGrounded)
                {
                    playerAnimation.TriggerTurnDustEffect(FacingDirection);
                }
                lastFacingDirection = FacingDirection;
                transform.localScale = new Vector3(FacingDirection, transform.localScale.y, transform.localScale.z);
            }
        }
    }

    private bool RaycastGroundAt(Vector2 origin, float distance, out RaycastHit2D hit)
    {
        hit = Physics2D.Raycast(origin, Vector2.down, distance, groundLayer);
        if (hit.collider == null || hit.normal.y <= groundCheckNormalThreshold) return false;
        if (hit.collider == currentPassThroughPlatform) return false;
        if (rb.linearVelocityY > 0f && hit.collider.GetComponent<PlatformEffector2D>() != null)
            return false;

        if (hit.collider.TryGetComponent(out AirOnlyCollisionPlatform platform) && !platform.AllowsSolidContactFrom(hit.normal))
            return false;

        return true;
    }

    private void GroundCheckUpdate()
    {
        Bounds bounds = cachedBounds;
        Vector2 leftFoot = new(bounds.min.x + edgeMargin, bounds.min.y + 0.02f);
        Vector2 rightFoot = new(bounds.max.x - edgeMargin, bounds.min.y + 0.02f);
        float dist = groundCheckDistance + 0.04f;

        bool rightGrounded = false;
        bool leftGrounded = false;

        if (RaycastGroundAt(leftFoot, dist, out RaycastHit2D leftHit))
        {
            isGrounded = true;
            currentSurfaceNormal = leftHit.normal;
            lastHitPoint = leftHit.point;

            leftGrounded = true;
            if (RaycastGroundAt(rightFoot, dist, out RaycastHit2D hit))
                rightGrounded = true;
        }
        else if (RaycastGroundAt(rightFoot, dist, out RaycastHit2D rightHit))
        {
            isGrounded = true;
            currentSurfaceNormal = rightHit.normal;
            lastHitPoint = rightHit.point;

            rightGrounded = true;
            if (RaycastGroundAt(leftFoot, dist, out RaycastHit2D hit))
                leftGrounded = true;
        }
        else
        {
            isGrounded = false;
        }

        if (isGrounded && leftGrounded && rightGrounded)
        {
            LastGroundedPos = transform.position;
        }
    }

    private void WallCheckUpdate()
    {
        onWall = false;
        if (isGrounded || IsMovementLockedBySkill || Mathf.Abs(horizontalInput) < InputDeadzone) return;

        Bounds bounds = cachedBounds;
        float dir = Mathf.Sign(horizontalInput);
        float rayLen = bounds.extents.x + wallCheckDistance;

        Vector2 head = new(bounds.center.x, bounds.max.y - (bounds.size.y * 0.1f));
        Vector2 chest = new(bounds.center.x, bounds.center.y + (bounds.extents.y * 0.2f));
        Vector2 waist = new(bounds.center.x, bounds.min.y + (bounds.size.y * 0.3f));

        int hits = 0;
        if (CheckWallRay(head, dir, rayLen)) hits++;
        if (CheckWallRay(chest, dir, rayLen)) hits++;
        if (CheckWallRay(waist, dir, rayLen)) hits++;

        if (hits >= 3) onWall = true;
    }

    private bool CheckWallRay(Vector2 origin, float dir, float len)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * dir, len, groundLayer);
        if (hit.collider == null || Mathf.Abs(hit.normal.x) <= wallCheckNormalThreshold) return false;
        if (hit.collider.TryGetComponent(out AirOnlyCollisionPlatform platform) && !platform.AllowsSolidContactFrom(hit.normal))
            return false;

        currentSurfaceNormal = hit.normal;
        lastHitPoint = hit.point;
        return true;
    }

    public bool IsAboutToLand(out RaycastHit2D hitInfo, float lookAheadDistance = 0.5f)
    {
        hitInfo = default;

        if (isGrounded || rb.linearVelocityY >= -0.1f) return false;

        Bounds bounds = cachedBounds;
        Vector2 leftFoot = new(bounds.min.x + edgeMargin, bounds.min.y);
        Vector2 rightFoot = new(bounds.max.x - edgeMargin, bounds.min.y);

        float dynamicDist = Mathf.Min(Mathf.Abs(rb.linearVelocityY) * Time.fixedDeltaTime + lookAheadDistance, 1.5f);

        if (RaycastGroundAt(leftFoot, dynamicDist, out hitInfo)) return true;
        if (RaycastGroundAt(rightFoot, dynamicDist, out hitInfo)) return true;

        return false;
    }

    // Every player collider except edge detection. The edge detection collider sits out in
    // front of the body, so including it stretches the bounds and throws off the wall,
    // ground and step-up checks. Built once, since the collider setup doesn't change.
    private Collider2D[] BuildBoundsColliders()
    {
        if (playerColliders == null || playerColliders.Length == 0)
            playerColliders = GetComponentsInChildren<Collider2D>(true);

        List<Collider2D> result = new List<Collider2D>();
        foreach (Collider2D col in playerColliders)
        {
            if (col == null) continue;
            if (col.GetComponentInParent<PlayerEdgeDetection>(true) != null) continue;
            result.Add(col);
        }

        return result.ToArray();
    }

    private Bounds ComputePlayerBounds()
    {
        if (boundsColliders == null || boundsColliders.Length == 0) boundsColliders = BuildBoundsColliders();
        if (boundsColliders.Length == 0) return new Bounds(transform.position, Vector3.one);

        //filtering out edge detection collider, I'm pretty sure I needed this for something else
        Bounds b = boundsColliders[0].bounds;
        for (int i = 1; i < boundsColliders.Length; i++) b.Encapsulate(boundsColliders[i].bounds);
        return b;
    }

    private bool TryPassThroughPlatform()
    {
        Bounds bounds = cachedBounds;
        RaycastHit2D hit = Physics2D.Raycast(bounds.center, Vector2.down, bounds.extents.y + 0.2f, groundLayer);

        if (hit.collider != null && hit.collider.GetComponent<PlatformEffector2D>() != null)
        {
            StartCoroutine(DisableCollisionRoutine(hit.collider));
            return true;
        }
        return false;
    }

    private IEnumerator DisableCollisionRoutine(Collider2D platform)
    {
        currentPassThroughPlatform = platform;

        foreach (var col in playerColliders) Physics2D.IgnoreCollision(col, platform, true);
        yield return new WaitForSeconds(passThroughPlatformDuration);
        foreach (var col in playerColliders) if (platform != null) Physics2D.IgnoreCollision(col, platform, false);

        if (currentPassThroughPlatform == platform) currentPassThroughPlatform = null;
    }

    private void PerformInventoryAction()
    {
        if (!inventoryPressed) return;
        inventoryPressed = false;

        if (inventoryDisplay == null)
            inventoryDisplay = UnityEngine.Object.FindFirstObjectByType<InventoryDisplay>(FindObjectsInactive.Include);

        if (inventoryDisplay != null && GameUI.UIGlobalInput.Instance != null)
        {
            GameUI.UIGlobalInput.Instance.ToggleWindow(inventoryDisplay);
        }
    }

    public void ApplyHazardKnockback(Vector2 contactPoint) =>
        ApplyHazardKnockback(contactPoint, hazardousKnockbackForce, hazardousStaggerDuration);

    private void OnCollisionEnter2D(Collision2D col) => HandleEnemyBodyCollision(col);

    private void HandleEnemyBodyCollision(Collision2D col)
    {
        if (!enemyBodyCollisionKnockback) return;
        if (((1 << col.gameObject.layer) & combat.enemyLayer) == 0) return;
        if (!col.collider.transform.root.TryGetComponent(out EnemyHealth _)) return;

        ApplyKnockback(col.transform.position, enemyBodyCollisionForce, applyStagger: true, playHurtAnimation: false);
    }

    public bool ApplyHazardKnockback(Vector2 contactPoint, float force, float staggerDuration, Vector2? directionOverride = null)
    {
        if (physicsSuspended || isStaggered || isBouncing || IsSkillActive) return false;

        bool wasPlunging = combat.WasRecentlyPlunging;

        CancelClimb();
        combat.ForceCancelAttack();

        rb.linearVelocity = Vector2.zero;
        Vector2 dir = directionOverride ?? new Vector2(transform.position.x >= contactPoint.x ? 1f : -1f, 1f);
        rb.AddForce(dir.normalized * force, ForceMode2D.Impulse);

        if (wasPlunging) PlayBounceState(bounceDuration);
        else StartHitStagger(staggerDuration);

        return true;
    }

    #endregion
}