using System.Collections;
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

    [Header("Gravity Settings")]
    [SerializeField] private float normGravity = 3f;
    [SerializeField] private float jumpGravity = 2.5f;
    [SerializeField] private float fallGravity = 4.5f;
    [SerializeField] private float plungeGravity = 10f;
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("Wall Settings")]
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] [Range(0f, 1f)] private float wallJumpCounterStrength = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float wallSlideUpwardDampening = 0.5f;
    [SerializeField] private float wallCheckNormalThreshold = 0.5f;
    [SerializeField] private Vector2 wallJumpForce = new(10f, 16f);
    [SerializeField] private float wallJumpDuration = 0.4f;
    [SerializeField] private float wallJumpBufferTime = 0.2f;

    [Header("Dash Settings")]
    [SerializeField] private float dashVelocity = 20f;
    [SerializeField] [Range(0.1f, 1f)] private float backDashMultiplier = 0.5f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCoolDown = 0.2f;

    [Header("Knockback Settings")]
    [SerializeField] private LayerMask hazardousLayers;
    [SerializeField] private float hazardousKnockbackForce = 12f;
    [SerializeField] private float hazardousStaggerDuration = 0.3f;
    [SerializeField] private float bounceDuration = 0.2f;
    public float BounceDuration => bounceDuration;
    [SerializeField] private float lightForce = 8f, lightStaggerDuration = 0.1f;
    [SerializeField] private float mediumForce = 10f, mediumStaggerDuration = 0.2f;
    [SerializeField] private float heavyForce = 14f, heavyStaggerDuration = 0.4f;

    [Header("Detection Settings")]
    public LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private float groundCheckNormalThreshold = 0.6f;
    [SerializeField] private float wallCheckDistance = 0.05f;
    [SerializeField] private float edgeMargin = 0.05f;

    private bool jumpPressed, jumpReleased, isGrounded, onWall, isWallSliding, isWallJumping;
    private bool dashPressed, dashReleased, isDashing, isStaggered, isBouncing;
    private bool isChargingSkillPhysics, isSkillGravityZeroed, isParryGravityActive, inventoryPressed;
    private const float InputDeadzone = 0.1f;

    private float horizontalInput, verticalInput, coyoteTimeCounter, wallCoyoteTimer, wallJumpTimer;
    private float dashTimer, dashDirection, wallJumpDirection;
    private bool wasWallSliding;
    private int movementFreezeCount;

    private PlayerAnimation playerAnimation;
    private PlayerCombatController combat;
    private Rigidbody2D rb;
    private Collider2D[] playerColliders;
    private InventoryDisplay inventoryDisplay;
    private Coroutine hitStaggerRoutine;
    private Coroutine bounceRoutine;
    private Bounds cachedBounds;

    public bool InputEnabled { get; set; } = true;
    public int FacingDirection { get; private set; } = 1;
    public bool IsMovementFrozen => movementFreezeCount > 0;
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
    public bool IsInvulnerable => IsNeutralDash;
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
        rb = GetComponent<Rigidbody2D>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
    }

    //private void Start() => rb.gravityScale = normGravity;
    private void Start()
    {
        rb.gravityScale = normGravity;
        lastFacingDirection = FacingDirection;
    }

    private void Update()
    {
        if (CanMove()) Flip();
        if (InputEnabled && !IsMovementFrozen) PerformInventoryAction();
    }

    private void FixedUpdate()
    {
        if (physicsSuspended) return;

        cachedBounds = ComputePlayerBounds();

        GroundCheckUpdate();
        WallCheckUpdate();

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

    private bool IsGravityZeroed => isParryGravityActive || isChargingSkillPhysics || isSkillGravityZeroed;

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
                             && !isWallJumping
                             && !isDashing
                             && !isStaggered
                             && !isWallSliding
                             && !IsMovementLockedBySkill
                             && !combat.IsPlunging;

    #region Movement Handlers

    private void HandleMovement()
    {
        if (IsMovementLockedBySkill || isStaggered) return;

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

    private void HandleJump()
    {
        bool canJump = InputEnabled
            && !isWallJumping
            && !isStaggered
            && !isWallSliding
            && !IsMovementLockedBySkill
            && !combat.IsPlunging;
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
        if (IsFrozenOrSkillLocked)
        {
            isWallSliding = false;
            return;
        }

        wallCoyoteTimer = (onWall && !isGrounded && Mathf.Abs(horizontalInput) > InputDeadzone) ? coyoteTime : wallCoyoteTimer - Time.fixedDeltaTime;

        if (onWall && !isGrounded && wallCoyoteTimer > 0f)
        {
            if (!isWallSliding)
            {
                combat.ForceCancelAttack();
                combat.CancelParry();
                if (combat.IsSkilling) combat.EndSkill();
            }

            isWallSliding = true;
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
        if (IsFrozenOrSkillLocked) return;

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
            if (combat.IsPlunging) return;
            if (isBouncing) return;
            if (combat.IsChargeInputHeld) return;
            if (combat.IsSkilling) return;
            if (combat.IsParrying) combat.CancelParry();
            if (IsMovementFrozen) return;
            if (combat.IsAttacking) combat.ForceCancelAttack();

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

            rb.linearVelocity = new Vector2(dashDirection * dashVelocity, rb.linearVelocity.y);
        
            playerAnimation.TriggerDashEffect();

            ConsumeDashInput();

            CancelInvoke(nameof(StopDashing));
            Invoke(nameof(StopDashing), activeDuration);
        }

        if (dashReleased) ConsumeDashInput();
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
        isDashing = false;
        isDashLocked = false;
    }

    private void HandlePlunge()
    {
        if (!combat.IsPlunging || isBouncing || isStaggered) return;
        if (rb.linearVelocityY > 0.1f) rb.linearVelocityY = 0f;
        rb.linearVelocityX = 0;
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

    public void ApplyKnockback(Vector2 sourcePosition, AttackForce attackForce, bool applyStagger = true)
    {
        if (physicsSuspended) return;

        combat.ForceCancelAttack();
        if(applyStagger) playerAnimation.PlayHurtAnimation();

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

        if(applyStagger) StartHitStagger(data.StaggerDuration);
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
        if (physicsSuspended) return; // NEW

        Vector2 dir = ((Vector2)transform.position - sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * force, ForceMode2D.Impulse);
    }

    public void TriggerBounce(Vector2 sourcePosition, float force, float duration)
    {
        if (physicsSuspended) return; // NEW

        combat.ForceCancelAttack();

        Vector2 dir = ((Vector2)transform.position - sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * force, ForceMode2D.Impulse);

        PlayBounceState(duration);
    }

    public void PlayBounceState(float duration)
    {
        if (physicsSuspended) return; // NEW

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
        verticalInput = raw.y;
        horizontalInput = Mathf.Abs(raw.x) > InputDeadzone ? Mathf.Sign(raw.x) * Mathf.Clamp01(raw.magnitude) : 0f;
    }

    public void OnJump(InputValue value) { jumpPressed = value.isPressed; jumpReleased = !value.isPressed; }
    public void OnDash(InputValue value) { dashPressed = value.isPressed; dashReleased = !value.isPressed; }
    public void OnInventory() => inventoryPressed = true;
    public void OnPause(InputValue value) { if (value.isPressed) GameManager.Instance?.TogglePause(); }

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

    #endregion

    #region Physics Checks & Utilities

    private void Flip()
    {
        if (Mathf.Abs(horizontalInput) > InputDeadzone)
        {
            // FacingDirection = horizontalInput > 0f ? 1 : -1;
            // transform.localScale = new Vector3(FacingDirection, transform.localScale.y, transform.localScale.z);
            int newFacingDirection = horizontalInput > 0f ? 1 : -1;

            if (newFacingDirection != FacingDirection)
            {
                FacingDirection = newFacingDirection;

                if (isGrounded)
                {
                    playerAnimation.TriggerTurnDustEffect(FacingDirection);
                }
                lastFacingDirection = FacingDirection;
                transform.localScale = new Vector3(FacingDirection,transform.localScale.y,transform.localScale.z);
            }
        }
    }

    private bool RaycastGroundAt(Vector2 origin, float distance, out RaycastHit2D hit)
    {
        hit = Physics2D.Raycast(origin, Vector2.down, distance, groundLayer);
        if (hit.collider == null || hit.normal.y <= groundCheckNormalThreshold) return false;

        if (hit.collider.TryGetComponent(out AirOnlyCollisionPlatform platform) && !platform.AllowsGroundCheckFrom(hit.normal))
            return false;

        return true;
    }

    private void GroundCheckUpdate()
    {
        Bounds bounds = cachedBounds;
        Vector2 leftFoot = new(bounds.min.x + edgeMargin, bounds.min.y + 0.02f);
        Vector2 rightFoot = new(bounds.max.x - edgeMargin, bounds.min.y + 0.02f);
        float dist = groundCheckDistance + 0.04f;

        if (RaycastGroundAt(leftFoot, dist, out RaycastHit2D leftHit))
        {
            isGrounded = true;
            currentSurfaceNormal = leftHit.normal;
            lastHitPoint = leftHit.point;
        }
        else if (RaycastGroundAt(rightFoot, dist, out RaycastHit2D rightHit))
        {
            isGrounded = true;
            currentSurfaceNormal = rightHit.normal;
            lastHitPoint = rightHit.point;
        }
        else
        {
            isGrounded = false;
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

        if (hits >= 2) onWall = true;
    }

    private bool CheckWallRay(Vector2 origin, float dir, float len)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * dir, len, groundLayer);
        if (hit.collider == null || Mathf.Abs(hit.normal.x) <= wallCheckNormalThreshold) return false;

        if (hit.collider.TryGetComponent(out AirOnlyCollisionPlatform platform) && !platform.AllowsWallSlideFrom(hit.normal)) // CHANGED
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

    private Bounds ComputePlayerBounds()
    {
        if (playerColliders == null || playerColliders.Length == 0) playerColliders = GetComponentsInChildren<Collider2D>(true);
        if (playerColliders == null || playerColliders.Length == 0) return new Bounds(transform.position, Vector3.one);

        Bounds b = playerColliders[0].bounds;
        for (int i = 1; i < playerColliders.Length; i++) b.Encapsulate(playerColliders[i].bounds);
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
        foreach (var col in playerColliders) Physics2D.IgnoreCollision(col, platform, true);
        yield return new WaitForSeconds(passThroughPlatformDuration);
        foreach (var col in playerColliders) if (platform != null) Physics2D.IgnoreCollision(col, platform, false);
    }

    private void PerformInventoryAction()
    {
        if (!inventoryPressed) return;
        inventoryPressed = false;

        if (inventoryDisplay == null) inventoryDisplay = UnityEngine.Object.FindFirstObjectByType<InventoryDisplay>();
        inventoryDisplay?.ToggleInventory();
    }

    private void OnCollisionEnter2D(Collision2D col) => HandleHazardousCollision(col);
    private void OnCollisionStay2D(Collision2D col) => HandleHazardousCollision(col);

    private void HandleHazardousCollision(Collision2D col)
    {
        if (((1 << col.gameObject.layer) & hazardousLayers) == 0 || isStaggered || isBouncing || IsSkillActive) return;
        if (combat.IsPlunging) return;

        bool isEnemyLayer = ((1 << col.gameObject.layer) & combat.enemyLayer) != 0;
        if (isEnemyLayer && !col.collider.transform.root.TryGetComponent(out EnemyHealth _)) return;

        bool wasPlunging = combat.IsPlunging || combat.WasRecentlyPlunging;

        combat.ForceCancelAttack();

        ContactPoint2D contact = col.GetContact(0);
        rb.linearVelocity = Vector2.zero;
        Vector2 dir = new Vector2(transform.position.x >= contact.point.x ? 1f : -1f, 1f).normalized;
        rb.AddForce(dir * hazardousKnockbackForce, ForceMode2D.Impulse);

        if (wasPlunging)
        {
            PlayBounceState(bounceDuration);
        }
        else
        {
            StartHitStagger(hazardousStaggerDuration);
        }
    }

    #endregion
}