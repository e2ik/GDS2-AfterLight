using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerInput))]
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

    [Header("Gravity Settings")]
    [SerializeField] private float normGravity = 3f;
    [SerializeField] private float jumpGravity = 2.5f;
    [SerializeField] private float fallGravity = 4.5f;
    [SerializeField] private float coyoteTime = 0.15f;

    [Header("Wall Settings")] 
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] [Range(0f, 1f)] private float wallJumpCounterStrength = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float wallSlideUpwardDampening = 0.5f;
    [SerializeField] private float wallCheckNormalThreshold = 0.5f;
    [SerializeField] private Vector2 wallJumpForce = new(10f, 16f);
    [SerializeField] private float wallJumpDuration = 0.4f;

    [Header("Dash Settings")]
    [SerializeField] private float dashVelocity = 20f;
    [SerializeField] [Range(0.1f, 1f)] private float backDashMultiplier = 0.5f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCoolDown = 0.2f;

    [Header("Knockback Settings")] 
    [SerializeField] private LayerMask hazardousLayers;
    [SerializeField] private float hazardousKnockbackForce = 12f;
    [SerializeField] private float hazardousStaggerDuration = 0.3f;
    [SerializeField] private float lightForce = 8f, lightStaggerDuration = 0.1f;
    [SerializeField] private float mediumForce = 10f, mediumStaggerDuration = 0.2f;
    [SerializeField] private float heavyForce = 14f, heavyStaggerDuration = 0.4f;

    [Header("Detection Settings")] 
    public LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private float wallCheckDistance = 0.05f;
    [SerializeField] private float edgeMargin = 0.05f;

    // Internal State Flags
    private bool jumpPressed, jumpReleased, isGrounded, onWall, isWallSliding, isWallJumping;
    private bool dashPressed, dashReleased, isDashing, isStaggered;
    private bool isChargingSkillPhysics, isSkillGravityZeroed, isParryGravityActive, inventoryPressed;
    
    private float horizontalInput, verticalInput, coyoteTimeCounter, wallCoyoteTimer, wallJumpTimer;
    private float dashTimer, dashDirection, wallJumpDirection;
    private bool wasWallSliding;
    private int movementFreezeCount;

    private Rigidbody2D rb;
    private Collider2D[] playerColliders;
    private PlayerCombatController combatController;
    private InventoryDisplay inventoryDisplay;
    private Coroutine hitStaggerRoutine;

    public bool InputEnabled { get; set; } = true;
    public int FacingDirection { get; private set; } = 1;
    public bool IsMovementFrozen => movementFreezeCount > 0;
    public bool IsGrounded => isGrounded;
    public bool IsWallSliding => isWallSliding;
    public bool IsDashing => isDashing;
    public bool IsChargingSkill => isChargingSkillPhysics;
    public bool IsDirectionalDash { get; private set; }
    public bool IsStaggered => isStaggered;

    private bool IsSkillActive => isChargingSkillPhysics || (combatController != null && combatController.IsSkilling);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerColliders = GetComponents<Collider2D>();
        combatController = GetComponent<PlayerCombatController>();
    }

    private void Start() => rb.gravityScale = normGravity;

    private void Update()
    {
        if (CanMove()) Flip();
        if (InputEnabled && !IsMovementFrozen) PerformInventoryAction();
    }

    private void FixedUpdate()
    {
        GroundCheckUpdate();
        WallCheckUpdate();

        HandleMovement();
        HandleWallSlide();
        HandleJump();
        HandleWallJump();
        HandleDash();

        UpdateGravity();
    }

    public void FreezeMovement(bool freeze)
    {
        movementFreezeCount = freeze ? movementFreezeCount + 1 : Mathf.Max(0, movementFreezeCount - 1);
        if (freeze) rb.linearVelocity = new Vector2(0f, rb.linearVelocityY);
    }

    public void SetParryGravity(bool active)
    {
        isParryGravityActive = active;
        if (active) rb.linearVelocity = Vector2.zero;
    }

    public void SetSkillCharging(bool active)
    {
        isChargingSkillPhysics = active;
        if (active)
        {
            rb.linearVelocity = Vector2.zero;
            isWallSliding = false;
        }
    }

    public void SetSkillGravityZero(bool active)
    {
        isSkillGravityZeroed = active;
        if (active)
        {
            rb.linearVelocity = Vector2.zero; // Halts momentum drift instantly
            rb.gravityScale = 0f;
        }
        else
        {
            UpdateGravity();
        }
    }

    public bool CanMove() => InputEnabled && !isWallJumping && !isDashing && !isChargingSkillPhysics && !isStaggered && !isWallSliding;

    #region Movement Handlers

    private void HandleMovement()
    {
        bool wallJumpNeutral = isWallJumping && Mathf.Abs(horizontalInput) < 0.01f;
        if ((!CanMove() && !isWallJumping) || wallJumpNeutral) return;

        float targetSpeed = horizontalInput * moveSpeed;
        float speedDif = targetSpeed - rb.linearVelocityX;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, velocityPower) * Mathf.Sign(speedDif);

        if (isWallJumping && horizontalInput != 0f && Mathf.Sign(horizontalInput) != Mathf.Sign(wallJumpDirection))
            movement *= wallJumpCounterStrength;

        rb.AddForce(movement * Vector2.right);

        if (isGrounded && Mathf.Abs(horizontalInput) < 0.01f)
        {
            float f = Mathf.Min(Mathf.Abs(rb.linearVelocityX), friction) * Mathf.Sign(rb.linearVelocityX);
            rb.AddForce(Vector2.right * -f, ForceMode2D.Impulse);
        }
    }

    private void HandleJump()
    {
        if (!CanMove()) return;

        coyoteTimeCounter = isGrounded ? coyoteTime : coyoteTimeCounter - Time.fixedDeltaTime;

        if (jumpPressed && coyoteTimeCounter > 0f && !isWallSliding)
        {
            if (verticalInput < -0.5f && TryPassThroughPlatform())
            {
                jumpPressed = jumpReleased = false;
                return;
            }

            if (!isGrounded) rb.linearVelocityY = 0f;
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpPressed = jumpReleased = false;
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
        if (IsMovementFrozen || IsSkillActive)
        {
            isWallSliding = false;
            return;
        }

        wallCoyoteTimer = (onWall && !isGrounded && Mathf.Abs(horizontalInput) > 0.1f) ? coyoteTime : wallCoyoteTimer - Time.fixedDeltaTime;

        if (onWall && !isGrounded && wallCoyoteTimer > 0f)
        {
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
        if (IsMovementFrozen) return;

        if (isWallSliding)
        {
            isWallJumping = false;
            if (!wasWallSliding) wallJumpDirection = -FacingDirection;
            wallJumpTimer = 0.2f;
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
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(new Vector2(wallJumpDirection * wallJumpForce.x, wallJumpForce.y), ForceMode2D.Impulse);
            wallJumpTimer = 0f;
            jumpPressed = jumpReleased = false;

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
        dashTimer = isDashing ? dashCoolDown : dashTimer - Time.deltaTime;

        if (dashPressed && isGrounded && dashTimer <= 0f)
        {
            if (combatController != null && combatController.IsParrying) combatController.CancelParry();
            else if (IsMovementFrozen) return;

            isDashing = true;
            float activeDuration = dashDuration;

            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                IsDirectionalDash = true;
                dashDirection = Mathf.Sign(horizontalInput);
            }
            else
            {
                IsDirectionalDash = false;
                dashDirection = -FacingDirection;
                activeDuration *= backDashMultiplier;
            }

            rb.linearVelocity = new Vector2(dashDirection * dashVelocity, rb.linearVelocity.y);
            dashPressed = dashReleased = false;

            CancelInvoke(nameof(StopDashing));
            Invoke(nameof(StopDashing), activeDuration);
        }

        if (dashReleased) dashPressed = dashReleased = false;
    }

    private void StopDashing() => isDashing = false;

    private void UpdateGravity()
    {
        if (isParryGravityActive || isChargingSkillPhysics || isSkillGravityZeroed)
        {
            rb.gravityScale = 0f;
        }
        else if (rb.linearVelocityY > 0.1f)
            rb.gravityScale = jumpGravity;
        else if (rb.linearVelocityY < -0.1f)
            rb.gravityScale = fallGravity;
        else
            rb.gravityScale = normGravity;
    }

    #endregion

    #region Damage & Stagger

    public void ApplyKnockback(Vector2 sourcePosition, AttackForce attackForce)
    {
        PlayerAnimation playerAnim = GetComponent<PlayerAnimation>();
        if (playerAnim != null) playerAnim.PlayHurtAnimation();
        else Debug.LogWarning("PlayerAnimation component not found on PlayerController. Cannot play hurt animation.");

        Vector2 forceData = attackForce switch
        {
            AttackForce.Light => new Vector2(lightForce, lightStaggerDuration),
            AttackForce.Medium => new Vector2(mediumForce, mediumStaggerDuration),
            AttackForce.Heavy => new Vector2(heavyForce, heavyStaggerDuration),
            _ => Vector2.zero
        };

        Vector2 dir = ((Vector2)transform.position - sourcePosition).normalized;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(dir * forceData.x, ForceMode2D.Impulse);

        if (hitStaggerRoutine != null) StopCoroutine(hitStaggerRoutine);
        hitStaggerRoutine = StartCoroutine(HitStaggerCoroutine(forceData.y));
    }

    private IEnumerator HitStaggerCoroutine(float duration)
    {
        isStaggered = true;
        yield return new WaitForSeconds(duration);
        isStaggered = false;
        hitStaggerRoutine = null;
    }

    #endregion

    #region Input Callbacks

    public void OnMove(InputValue value)
    {
        Vector2 raw = value.Get<Vector2>();
        verticalInput = raw.y;
        horizontalInput = Mathf.Abs(raw.x) > 0.1f ? Mathf.Sign(raw.x) * Mathf.Clamp01(raw.magnitude) : 0f;
    }

    public void OnJump(InputValue value) { jumpPressed = value.isPressed; jumpReleased = !value.isPressed; }
    public void OnDash(InputValue value) { dashPressed = value.isPressed; dashReleased = !value.isPressed; }
    public void OnInventory() => inventoryPressed = true;
    public void OnPause(InputValue value) { if (value.isPressed) GameManager.Instance?.TogglePause(); }

    #endregion

    #region Physics Checks & Utilities

    private void Flip()
    {
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            FacingDirection = horizontalInput > 0f ? 1 : -1;
            transform.localScale = new Vector3(FacingDirection, transform.localScale.y, transform.localScale.z);
        }
    }

    private void GroundCheckUpdate()
    {
        Bounds bounds = GetPlayerBounds();
        Vector2 leftFoot = new Vector2(bounds.min.x + edgeMargin, bounds.min.y + 0.02f);
        Vector2 rightFoot = new Vector2(bounds.max.x - edgeMargin, bounds.min.y + 0.02f);
        float dist = groundCheckDistance + 0.04f;

        RaycastHit2D leftHit = Physics2D.Raycast(leftFoot, Vector2.down, dist, groundLayer);
        RaycastHit2D rightHit = Physics2D.Raycast(rightFoot, Vector2.down, dist, groundLayer);

        isGrounded = (leftHit.collider != null && leftHit.normal.y > 0.6f) || (rightHit.collider != null && rightHit.normal.y > 0.6f);
    }

    private void WallCheckUpdate()
    {
        onWall = false;
        if (isGrounded || IsSkillActive || Mathf.Abs(horizontalInput) < 0.1f) return;

        Bounds bounds = GetPlayerBounds();
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
        return hit.collider != null && Mathf.Abs(hit.normal.x) > wallCheckNormalThreshold;
    }

    private Bounds GetPlayerBounds()
    {
        if (playerColliders == null || playerColliders.Length == 0) playerColliders = GetComponents<Collider2D>();
        if (playerColliders == null || playerColliders.Length == 0) return new Bounds(transform.position, Vector3.one);

        Bounds b = playerColliders[0].bounds;
        for (int i = 1; i < playerColliders.Length; i++) b.Encapsulate(playerColliders[i].bounds);
        return b;
    }

    private bool TryPassThroughPlatform()
    {
        Bounds bounds = GetPlayerBounds();
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
        yield return new WaitForSeconds(0.25f);
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
        if (((1 << col.gameObject.layer) & hazardousLayers) == 0 || isStaggered || IsSkillActive) return;

        ContactPoint2D contact = col.GetContact(0);
        rb.linearVelocity = Vector2.zero;
        Vector2 dir = new Vector2(transform.position.x >= contact.point.x ? 1f : -1f, 1f).normalized;
        rb.AddForce(dir * hazardousKnockbackForce, ForceMode2D.Impulse);

        if (hitStaggerRoutine != null) StopCoroutine(hitStaggerRoutine);
        hitStaggerRoutine = StartCoroutine(HitStaggerCoroutine(hazardousStaggerDuration));
    }

    #endregion
}