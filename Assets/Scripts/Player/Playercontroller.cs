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
    [SerializeField, Min(0.02f)] private float jumpBufferTime = 0.1f;
    [SerializeField, Min(0f)] private float jumpEffectHoldTime = 0.08f;
    [SerializeField, Min(0f)] private float turnEffectHoldTime = 0.08f;
    [SerializeField, Min(0f)] private float turnEffectMinSpeed = 2f;
    [SerializeField, Min(0f)] private float turnEffectCooldown = 0.3f;
    [SerializeField, Min(0f)] private float turnEffectMinDistance = 0.5f;
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
    [SerializeField] private bool directionalDashInvulnerable = true;
    [SerializeField] private float perfectDodgeWindow = 0.1f;
    private float neutralDashStartTime = float.NegativeInfinity;
    private float neutralDashInvulnTimer;

    [Header("Knockback Settings")]
    [SerializeField] private bool enemyBodyCollisionKnockback = true;
    [SerializeField] private AttackForce enemyBodyCollisionForce = AttackForce.Light;
    [SerializeField] private float enemyBodyAirKnockbackForce = 8f;
    [SerializeField] private float enemyBodyAirStaggerDuration = 0.1f;
    [SerializeField, Range(0f, 90f)] private float enemyBodyAirKnockbackAngle = 45f;
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
    [SerializeField] private bool climbEndFeetOnLedge = true;
    [SerializeField, Min(0f)] private float climbEndClearance = 0.01f;
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
    [SerializeField] private bool snapToGroundOnStart = true;
    [SerializeField, Min(0f)] private float groundSnapMaxDistance = 10f;
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private float groundCheckNormalThreshold = 0.6f;
    [SerializeField] private float wallCheckDistance = 0.05f;
    [SerializeField] private float edgeMargin = 0.05f;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private bool boundsIgnoreTriggers = false;
    [SerializeField] private bool drawDetectionGizmos = true;

    private bool jumpPressed, jumpReleased, isGrounded, onWall, isWallSliding, isWallJumping;
    private bool dashPressed, dashReleased, isDashing, isStaggered, isBouncing;
    private bool isChargingSkillPhysics, isSkillGravityZeroed, isParryGravityActive, inventoryPressed;
    private bool healPressed, isHealing;
    private const float InputDeadzone = 0.1f;
    private Collider2D currentPassThroughPlatform;

    private float horizontalInput, verticalInput, coyoteTimeCounter, wallCoyoteTimer, wallJumpTimer, wallContactTimer;
    private float dashTimer, dashDirection, wallJumpDirection, wallSide;
    private bool wasWallSliding;
    private float jumpBufferTimer;
    private bool jumpHeld;
    private bool jumpEffectPending;
    private float jumpEffectTimer;
    private Vector2 jumpEffectPosition;
    private Vector2 jumpEffectNormal;
    private bool turnEffectPending;
    private float turnEffectTimer;
    private int turnEffectDirection;
    private float lastTurnEffectTime = float.NegativeInfinity;
    private float lastTurnEffectX = float.PositiveInfinity;
    private int movementFreezeCount;

    private PlayerAnimation playerAnimation;
    private PlayerCombatController combat;
    private PlayerHeals heals;
    private PlayerStats stats;
    private Rigidbody2D rb;
    private Collider2D[] playerColliders;
    private Collider2D[] boundsColliders;
    private InventoryDisplay inventoryDisplay;
    private Coroutine hitStaggerRoutine;
    private Coroutine bounceRoutine;
    private Bounds cachedBounds;
    private Vector2 LastGroundedPos { get; set; }

    public bool InputEnabled { get; set; } = true;
    public bool IsScriptedWalking => scriptedWalkActive;

    private bool scriptedWalkActive;
    private float scriptedWalkTargetX;
    private float scriptedWalkSpeed;
    private float scriptedWalkTimer;
    private bool scriptedWalkRestoreInput;
    private System.Action scriptedWalkArrived;
    private float lastMoveHorizontal;
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
    public bool IsInPerfectDodgeWindow => IsNeutralDash && !isDashLocked && Time.time - neutralDashStartTime <= perfectDodgeWindow;
    public bool IsNeutralDashInvulnerable => IsNeutralDash || neutralDashInvulnTimer > 0f;
    public bool IsDirectionalDashInvulnerable => directionalDashInvulnerable && isDashing && IsDirectionalDash && !isDashLocked;
    public bool IsInvulnerable => IsNeutralDashInvulnerable || IsDirectionalDashInvulnerable;
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
        stats = player.Stats;
        rb = GetComponent<Rigidbody2D>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
        boundsColliders = BuildBoundsColliders();
        edgeDetection = GetComponentInChildren<PlayerEdgeDetection>(true);
    }

    private void Start()
    {
        rb.gravityScale = normGravity;
        lastFacingDirection = FacingDirection;

        if (snapToGroundOnStart) SnapToGround();
    }

    public bool SnapToGround(float maxDistance = -1f)
    {
        if (rb == null) return false;
        if (maxDistance <= 0f) maxDistance = groundSnapMaxDistance;

        bool restoreDisabledColliders = physicsSuspended;
        if (restoreDisabledColliders) SetCollidersEnabled(true);

        try
        {
            return SnapToGroundInternal(maxDistance);
        }
        finally
        {
            if (restoreDisabledColliders) SetCollidersEnabled(false);
        }
    }

    private bool SnapToGroundInternal(float maxDistance)
    {
        Physics2D.SyncTransforms();
        Bounds bounds = ComputePlayerBounds();

        Vector2 origin = new Vector2(bounds.center.x, bounds.center.y);
        float castDistance = bounds.extents.y + maxDistance;

        bool previousStartInColliders = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = false;
        RaycastHit2D hit = RaycastIgnoringSelf(origin, Vector2.down, castDistance);
        Physics2D.queriesStartInColliders = previousStartInColliders;

        if (hit.collider == null || hit.normal.y <= groundCheckNormalThreshold) return false;

        float offset = hit.point.y - bounds.min.y;
        Vector3 position = transform.position + new Vector3(0f, offset, 0f);

        transform.position = position;
        rb.position = position;
        rb.linearVelocity = Vector2.zero;

        Physics2D.SyncTransforms();
        cachedBounds = ComputePlayerBounds();
        GroundCheckUpdate();
        return true;
    }

    private void Update()
    {
        if (neutralDashInvulnTimer > 0f) neutralDashInvulnTimer -= Time.deltaTime;

        if (!IsUILocked && CanMove()) Flip();
        UpdatePendingTurnEffect();
        if (InputEnabled && !IsMovementFrozen)
        {
            PerformInventoryAction(); 
            HandleHeal();
        }
        else
        {
            inventoryPressed = false;
            healPressed = false;
        }

        if (!InputEnabled)
        {
            jumpPressed = false;
            dashPressed = false;
        }
    }

    private void FixedUpdate()
    {
        if (physicsSuspended) return;

        cachedBounds = ComputePlayerBounds();

        if (jumpPressed)
        {
            jumpBufferTimer -= Time.fixedDeltaTime;
            if (jumpBufferTimer < 0f) jumpPressed = false;
        }

        UpdatePendingJumpEffect();

        GroundCheckUpdate();
        WallCheckUpdate();

        HandleEdgeClimb();
        HandleStepUp();
        UpdateScriptedWalk();
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

        if (freeze)
        {
            if (isDashing && !isDashLocked)
            {
                CancelInvoke(nameof(StopDashing));
                StopDashing();
            }

            rb.linearVelocity = new Vector2(0f, rb.linearVelocityY);
            jumpPressed = false;
            dashPressed = false;
        }
        else if (movementFreezeCount == 0)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocityY);
            jumpPressed = false;
            dashPressed = false;
        }
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

    public bool CanMove() => (InputEnabled || scriptedWalkActive)
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
        RaycastHit2D lowHit = RaycastIgnoringSelf(lowOrigin, Vector2.right * dir, stepCheckDistance);
        if (lowHit.collider == null) return;

        Vector2 highOrigin = new(lowOrigin.x, bounds.min.y + maxStepHeight);
        RaycastHit2D highHit = RaycastIgnoringSelf(highOrigin, Vector2.right * dir, stepCheckDistance);
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
                Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerDroppedThrough);
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

            if (!isGrounded || rb.linearVelocityY < 0f) rb.linearVelocityY = 0f;
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

            QueueJumpEffect();
            Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerJumped);

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

    private void QueueJumpEffect()
    {
        if (!jumpHeld) return;

        Bounds bounds = cachedBounds;
        jumpEffectPosition = new Vector2(bounds.center.x, bounds.min.y);
        jumpEffectNormal = currentSurfaceNormal;
        jumpEffectTimer = jumpEffectHoldTime;
        jumpEffectPending = true;

        if (jumpEffectHoldTime <= 0f) UpdatePendingJumpEffect();
    }

    private void UpdatePendingJumpEffect()
    {
        if (!jumpEffectPending) return;

        if (!jumpHeld)
        {
            jumpEffectPending = false;
            return;
        }

        jumpEffectTimer -= Time.fixedDeltaTime;
        if (jumpEffectTimer > 0f) return;

        jumpEffectPending = false;
        playerAnimation.TriggerJumpEffectAt(jumpEffectPosition, jumpEffectNormal);
    }

    private void QueueTurnEffect()
    {
        turnEffectPending = false;
        if (Mathf.Abs(rb.linearVelocityX) < turnEffectMinSpeed) return;

        turnEffectDirection = FacingDirection;
        turnEffectTimer = turnEffectHoldTime;
        turnEffectPending = true;

        if (turnEffectHoldTime <= 0f) UpdatePendingTurnEffect();
    }

    private void UpdatePendingTurnEffect()
    {
        if (!turnEffectPending) return;

        bool stillTurned = FacingDirection == turnEffectDirection
                           && isGrounded
                           && Mathf.Abs(horizontalInput) > InputDeadzone;
        if (!stillTurned)
        {
            turnEffectPending = false;
            return;
        }

        turnEffectTimer -= Time.deltaTime;
        if (turnEffectTimer > 0f) return;

        turnEffectPending = false;

        if (Time.time - lastTurnEffectTime < turnEffectCooldown) return;
        if (Mathf.Abs(rb.position.x - lastTurnEffectX) < turnEffectMinDistance) return;

        lastTurnEffectTime = Time.time;
        lastTurnEffectX = rb.position.x;
        playerAnimation.TriggerTurnDustEffect(turnEffectDirection);
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

        bool wallJustLeft = isWallJumping && wallSide == -wallJumpDirection;
        bool touchingWall = onWall && !isGrounded && wallCoyoteTimer > 0f && !wallJustLeft;
        wallContactTimer = touchingWall ? wallContactTimer + Time.fixedDeltaTime : 0f;

        if (touchingWall && wallContactTimer >= wallSlideGracePeriod)
        {
            if (!isWallSliding)
            {
                if (FacingDirection != (int)wallSide)
                {
                    FacingDirection = (int)wallSide;
                    transform.localScale = new Vector3(FacingDirection, transform.localScale.y, transform.localScale.z);
                }

                combat.ForceCancelAttack();
                combat.CancelParry();
                if (combat.IsSkilling) combat.EndSkill();
                Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerWallSlid);
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
            if (!wasWallSliding) wallJumpDirection = -wallSide;
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
            Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerWallJumped);
            combat.ForceCancelAttack();
            combat.NotifyJumpInputReceived();
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(new Vector2(wallJumpDirection * wallJumpForce.x, wallJumpForce.y), ForceMode2D.Impulse);
            currentSurfaceNormal = new Vector2(-wallJumpDirection, 0f);

            playerAnimation.TriggerJumpEffect(true, wallJumpDirection);

            wallJumpTimer = 0f;
            wallContactTimer = 0f;
            isWallSliding = false;
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
            if (isHealing) return;
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
                neutralDashStartTime = Time.time;
                dashDirection = -FacingDirection;
                activeDuration *= backDashMultiplier;
            }

            IgnoreAllAirOnlyPlatformsDuringDash(activeDuration);

            rb.linearVelocity = new Vector2(dashDirection * dashVelocity, rb.linearVelocity.y);

            playerAnimation.TriggerDashEffect();
            Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerDashed);
            if (!IsDirectionalDash) Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerBackDashed);

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
        Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerClimbed);
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

        if (climbEndFeetOnLedge)
        {
            float feetOffset = rb.position.y - cachedBounds.min.y;
            climbEndPos.y = corner.y + feetOffset + climbEndClearance;
        }

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
    public event System.Action OnHealStarted;
    public event System.Action OnHealDenied;

    private void HandleHeal()
    {
        if (!healPressed) return;
        healPressed = false;

        if (isHealing) return;

        if (heals != null && heals.GetCurrentHealCount() <= 0)
        {
            OnHealDenied?.Invoke();
            return;
        }

        if (!CanMove() || !combat.CanHeal || heals == null || (stats != null && stats.CurrentHealth >= stats.MaxHealth)) return;

        isHealing = true;
        rb.linearVelocityX = 0;
        OnHealStarted?.Invoke();
        Tutorial.TutorialEvents.Raise(Tutorial.TutorialEvents.PlayerHealed);
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
        if (playerAnimation != null) playerAnimation.FlashOnHeal();
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
        CancelHeal();
        combat.ForceCancelAttack();
        if (applyStagger && playHurtAnimation) playerAnimation.PlayHurtAnimation();

        KnockbackData data = attackForce switch
        {
            AttackForce.Light => new KnockbackData(lightForce, lightStaggerDuration),
            AttackForce.Medium => new KnockbackData(mediumForce, mediumStaggerDuration),
            AttackForce.Heavy => new KnockbackData(heavyForce, heavyStaggerDuration),
            _ => new KnockbackData(0f, 0f)
        };

        PushAway(sourcePosition, data.Force, data.StaggerDuration, applyStagger);
    }

    public void ApplyKnockback(Vector2 sourcePosition, float force, float staggerDuration, bool applyStagger = true, bool playHurtAnimation = true, Vector2? directionOverride = null)
    {
        if (physicsSuspended) return;

        CancelClimb();
        CancelHeal();
        combat.ForceCancelAttack();
        if (applyStagger && playHurtAnimation) playerAnimation.PlayHurtAnimation();

        PushAway(sourcePosition, force, staggerDuration, applyStagger, directionOverride);
    }

    private void PushAway(Vector2 sourcePosition, float force, float staggerDuration, bool applyStagger, Vector2? directionOverride = null)
    {
        Vector2 dir = directionOverride.HasValue
            ? directionOverride.Value.normalized
            : ((Vector2)transform.position - sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * force, ForceMode2D.Impulse);

        if (applyStagger && staggerDuration > 0f) StartHitStagger(staggerDuration);
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
        CancelHeal();
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

        lastMoveHorizontal = Mathf.Abs(raw.x) > InputDeadzone
            ? Mathf.Sign(raw.x) * Mathf.Clamp01(raw.magnitude)
            : 0f;

        if (!scriptedWalkActive) horizontalInput = lastMoveHorizontal;
    }

    public void StartScriptedWalk(float targetX, System.Action onArrived = null, float speedMultiplier = 1f, float timeout = 3f)
    {
        if (scriptedWalkActive) FinishScriptedWalk(false);

        scriptedWalkActive = true;
        scriptedWalkTargetX = targetX;
        scriptedWalkSpeed = Mathf.Clamp01(speedMultiplier);
        scriptedWalkTimer = timeout;
        scriptedWalkArrived = onArrived;
        scriptedWalkRestoreInput = InputEnabled;

        InputEnabled = false;
        jumpPressed = false;
        dashPressed = false;
    }

    public void CancelScriptedWalk()
    {
        if (scriptedWalkActive) FinishScriptedWalk(false);
    }

    private void UpdateScriptedWalk()
    {
        if (!scriptedWalkActive) return;

        scriptedWalkTimer -= Time.fixedDeltaTime;
        float dx = scriptedWalkTargetX - rb.position.x;

        if (Mathf.Abs(dx) <= 0.1f || scriptedWalkTimer <= 0f)
        {
            FinishScriptedWalk(true);
            return;
        }

        horizontalInput = Mathf.Sign(dx) * scriptedWalkSpeed;
    }

    private void FinishScriptedWalk(bool invokeCallback)
    {
        scriptedWalkActive = false;
        horizontalInput = 0f;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocityY);

        if (scriptedWalkRestoreInput) InputEnabled = true;
        horizontalInput = InputEnabled ? lastMoveHorizontal : 0f;

        System.Action callback = scriptedWalkArrived;
        scriptedWalkArrived = null;
        if (invokeCallback) callback?.Invoke();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed && (IsUILocked || !InputEnabled)) return;

        if (value.isPressed)
        {
            combat.CancelParry();
            jumpPressed = true;
            jumpReleased = false;
            jumpHeld = true;
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpReleased = true;
            jumpHeld = false;
        }
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed && (IsUILocked || !InputEnabled)) return;

        dashPressed = value.isPressed; dashReleased = !value.isPressed;
    }

    public void OnInventory()
    {
        if (!InputEnabled) return;
        inventoryPressed = true;
    }

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
    
    private void OnHeal()
    {
        if (!InputEnabled) return;
        healPressed = true;
    }

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
                    QueueTurnEffect();
                }
                lastFacingDirection = FacingDirection;
                transform.localScale = new Vector3(FacingDirection, transform.localScale.y, transform.localScale.z);
            }
        }
    }

    private readonly RaycastHit2D[] raycastBuffer = new RaycastHit2D[8];

    private RaycastHit2D RaycastIgnoringSelf(Vector2 origin, Vector2 direction, float distance)
    {
        int count = Physics2D.RaycastNonAlloc(origin, direction, raycastBuffer, distance, groundLayer);

        RaycastHit2D closest = default;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = raycastBuffer[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance >= closestDistance) continue;

            closest = hit;
            closestDistance = hit.distance;
        }

        return closest;
    }

    private bool RaycastGroundAt(Vector2 origin, float distance, out RaycastHit2D hit)
    {
        hit = RaycastIgnoringSelf(origin, Vector2.down, distance);
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

        if (hits >= 3)
        {
            onWall = true;
            wallSide = dir;
        }
    }

    private bool CheckWallRay(Vector2 origin, float dir, float len)
    {
        RaycastHit2D hit = RaycastIgnoringSelf(origin, Vector2.right * dir, len);
        if (hit.collider == null || Mathf.Abs(hit.normal.x) <= wallCheckNormalThreshold) return false;
        if (hit.collider.TryGetComponent(out AirOnlyCollisionPlatform platform) && !platform.AllowsSolidContactFrom(hit.normal))
            return false;

        currentSurfaceNormal = hit.normal;
        lastHitPoint = hit.point;
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawDetectionGizmos || !Application.isPlaying) return;

        Bounds bounds = cachedBounds;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(bounds.center, bounds.size);

        float dir = Mathf.Abs(horizontalInput) > InputDeadzone ? Mathf.Sign(horizontalInput) : FacingDirection;
        float rayLen = bounds.extents.x + wallCheckDistance;

        Vector2 head = new(bounds.center.x, bounds.max.y - (bounds.size.y * 0.1f));
        Vector2 chest = new(bounds.center.x, bounds.center.y + (bounds.extents.y * 0.2f));
        Vector2 waist = new(bounds.center.x, bounds.min.y + (bounds.size.y * 0.3f));

        DrawWallRayGizmo(head, dir, rayLen);
        DrawWallRayGizmo(chest, dir, rayLen);
        DrawWallRayGizmo(waist, dir, rayLen);
    }

    private void DrawWallRayGizmo(Vector2 origin, float dir, float len)
    {
        RaycastHit2D hit = RaycastIgnoringSelf(origin, Vector2.right * dir, len);
        bool valid = hit.collider != null && Mathf.Abs(hit.normal.x) > wallCheckNormalThreshold;

        Gizmos.color = valid ? Color.green : (hit.collider != null ? new Color(1f, 0.5f, 0f) : Color.red);
        Vector2 end = hit.collider != null ? hit.point : origin + Vector2.right * dir * len;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, 0.03f);
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
        if (bodyCollider != null) return new[] { bodyCollider };

        if (playerColliders == null || playerColliders.Length == 0)
            playerColliders = GetComponentsInChildren<Collider2D>(true);

        PlayerEdgeDetection detector = GetComponentInChildren<PlayerEdgeDetection>(true);
        Transform detectorRoot = detector != null && detector.gameObject != gameObject ? detector.transform : null;

        List<Collider2D> result = new List<Collider2D>();
        foreach (Collider2D col in playerColliders)
        {
            if (col == null) continue;
            if (detectorRoot != null && col.transform.IsChildOf(detectorRoot)) continue;
            if (boundsIgnoreTriggers && col.isTrigger) continue;
            result.Add(col);
        }

        return result.ToArray();
    }

    private Bounds ComputePlayerBounds()
    {
        if (boundsColliders == null || boundsColliders.Length == 0) boundsColliders = BuildBoundsColliders();
        if (boundsColliders.Length == 0) return new Bounds(transform.position, Vector3.one);

        //filtering out edge detection collider, I'm pretty sure I needed this for something else
        bool found = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);

        foreach (Collider2D col in boundsColliders)
        {
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

            if (!found)
            {
                b = col.bounds;
                found = true;
            }
            else
            {
                b.Encapsulate(col.bounds);
            }
        }

        return found ? b : new Bounds(transform.position, Vector3.one);
    }

    private bool TryPassThroughPlatform()
    {
        Bounds bounds = cachedBounds;
        RaycastHit2D hit = RaycastIgnoringSelf(bounds.center, Vector2.down, bounds.extents.y + 0.2f);

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
            bool anyPanelOpen = GameUI.UIManager.Instance != null && GameUI.UIManager.Instance.HasOpenWindows;
            if (anyPanelOpen && InputManager.IsUsingGamepad) return;
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

        if (isGrounded)
            ApplyKnockback(col.transform.position, enemyBodyCollisionForce, applyStagger: true, playHurtAnimation: false);
        else
        {
            float away = transform.position.x >= col.transform.position.x ? 1f : -1f;
            float radians = enemyBodyAirKnockbackAngle * Mathf.Deg2Rad;
            Vector2 airDirection = new Vector2(away * Mathf.Cos(radians), Mathf.Sin(radians));
            ApplyKnockback(col.transform.position, enemyBodyAirKnockbackForce, enemyBodyAirStaggerDuration, applyStagger: true, playHurtAnimation: false, directionOverride: airDirection);
        }
    }

    public bool ApplyHazardKnockback(Vector2 contactPoint, float force, float staggerDuration, Vector2? directionOverride = null)
    {
        if (physicsSuspended || isStaggered || isBouncing || IsSkillActive) return false;

        bool wasPlunging = combat.WasRecentlyPlunging;

        CancelClimb();
        CancelHeal();
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