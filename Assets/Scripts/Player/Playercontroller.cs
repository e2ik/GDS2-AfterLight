using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float tapJumpMultiplier = 0.2f;
    
    private float acceleration = 8f;
    private float deceleration = 8f;
    private float velocityPower = 1.2f;
    private float friction = 0.2f;
    
    [SerializeField] private float normGravity;
    [SerializeField] private float jumpGravity;
    [SerializeField] private float fallGravity;
    
    [SerializeField] private float coyoteTime = 0.15f;
    private bool jumpPressed;
    private bool jumpReleased;
    private float coyoteTimeCounter;
    
    [Header("Wall Movement")] 
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] [Range(0f, 1f)] private float wallJumpCounterStrength = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float wallSlideUpwardDampening = 0.5f;
    [SerializeField] private float wallCheckNormalThreshold = 0.5f;
    private bool isWallSliding;
    private bool isWallJumping;
    private float wallJumpDirection;
    private float wallJumpLeniency = 0.2f;
    private float wallJumpTimer;
    private float wallJumpDuration = 0.4f;
    private Vector2 wallJumpForce = new(10f, 16f);
    
    [Header("Dash Movement")]
    [SerializeField] private float dashVelocity = 20f;
    private float dashDuration = 0.2f;
    private bool dashPressed;
    private bool dashReleased;
    private bool isDashing;
    private float dashDirection;
    private float dashCoolDown = 0.2f;
    private float dashTimer;

    [Header("Skill Charge Movement")] 
    private bool skillPressed;
    private bool skillReleased;
    private bool isChargingSkill;
    private float chargingSkillTimer;
    private float chargingSkillMinDur = 0.4f;
    private float chargingSkillMaxDur;

    private bool inventoryPressed;
    private bool interactPressed;

    [Header("Detection Settings")] 
    public LayerMask groundLayer;
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private float wallCheckDistance = 0.05f;
    [SerializeField] private float edgeMargin = 0.05f;
    
    private bool isGrounded;
    private bool onWall;
    private Vector2 lastWallNormal;
    
    private Rigidbody2D rb;
    private Collider2D[] playerColliders;
    private PlayerEquipmentManager equipmentManager;

    private InputAction moveAction;
    private InputAction attackAction;
    private InputAction sAttackAction;
    private InputAction inventoryAction;
    private float horizontalInput;
    private Vector2 jumpInput;

    // Movement Freeze State
    private int movementFreezeCount = 0;
    private bool isStaggered;
    public bool IsMovementFrozen => movementFreezeCount > 0;

    public bool InputEnabled { get; set; } = true;    
    public int FacingDirection { get; private set; } = 1;

    // Anim required
    public bool IsGrounded => isGrounded;
    public bool IsWallSliding => isWallSliding;
    public bool IsDashing => isDashing;
    public bool IsChargingSkill => isChargingSkill;
    public bool IsDirectionalDash { get; private set; }
    private bool isParryGravityActive;
    public bool IsStaggered => isStaggered;

    private Coroutine hitStaggerRoutine;
    [SerializeField] private float defaultStaggerDur = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerColliders = GetComponents<Collider2D>();
        equipmentManager = GetComponent<PlayerEquipmentManager>();

        PlayerInput playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        attackAction = playerInput.actions["Attack"];
        sAttackAction = playerInput.actions["SAttack"];
        inventoryAction = playerInput.actions["Inventory"];
    }

    private void Start()
    {
        rb.gravityScale = normGravity;
    }
    
    private void Update()
    {
        if (CanMove())
        {
            Flip();
        }
        if (!InputEnabled || IsMovementFrozen) return;

        PerformInventoryAction();
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
            
        GravityState();
    }

    public void FreezeMovement(bool freeze)
    {
        if (freeze)
        {
            movementFreezeCount++;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocityY);
        }
        else
        {
            movementFreezeCount = Mathf.Max(0, movementFreezeCount - 1);
        }
    }

    public void SetParryGravity(bool active)
    {
        isParryGravityActive = active;
        if (active)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
    
    private bool CanMove()
    {
        return InputEnabled && !IsMovementFrozen && !isWallJumping && !isDashing && !isChargingSkill && !isStaggered && !isWallSliding;
    }

    public void HitStagger()
    {
        if (hitStaggerRoutine != null)
            StopCoroutine(hitStaggerRoutine);
        hitStaggerRoutine = StartCoroutine(HitStaggerCoroutine(defaultStaggerDur));
    }

    public void HitStagger(float duration)
    {
        if (hitStaggerRoutine != null)
            StopCoroutine(hitStaggerRoutine);
        hitStaggerRoutine = StartCoroutine(HitStaggerCoroutine(duration));
    }

    private IEnumerator HitStaggerCoroutine(float duration)
    {
        isStaggered = true;
        yield return new WaitForSeconds(duration);
        isStaggered = false;
        hitStaggerRoutine = null;
    }
    
    private void HandleMovement()
    {
        bool wallJumpNeutral = isWallJumping && Mathf.Abs(horizontalInput) < 0.01f;

        if ((!CanMove() && !isWallJumping) || wallJumpNeutral) return;

        float targetSpeed = horizontalInput * moveSpeed;
        float speedDif = targetSpeed - rb.linearVelocityX;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, velocityPower) * Mathf.Sign(speedDif);

        if (isWallJumping && horizontalInput != 0f && Mathf.Sign(horizontalInput) != Mathf.Sign(wallJumpDirection))
        {
            movement *= wallJumpCounterStrength;
        }

        rb.AddForce(movement * Vector2.right);

        if (isGrounded && horizontalInput < 0.01f)
        {
            float f = Mathf.Min(Mathf.Abs(rb.linearVelocityX), Mathf.Abs(friction));
            f *= Mathf.Sign(rb.linearVelocityX);
            rb.AddForce(Vector2.right * -f, ForceMode2D.Impulse);
        }
    }
    
    private void HandleJump()
    {
        if (!CanMove()) return;
        
        if (isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.fixedDeltaTime;
        
        if (jumpPressed && coyoteTimeCounter > 0f && !isWallSliding)
        {
            if (!isGrounded) rb.linearVelocityY = 0f;
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            jumpPressed = false;
            jumpReleased = false;
            coyoteTimeCounter = 0f;
        }
        if (jumpReleased)
        {
            if (rb.linearVelocityY > 0)
            {
                rb.AddForce(Vector2.down * (rb.linearVelocityY * (1 - tapJumpMultiplier)), ForceMode2D.Impulse);
            }
            jumpReleased = false;
        }
    }

    private float wallCoyoteTimer;
    private bool wasWallSliding;

    private void HandleWallSlide()
    {
        if (IsMovementFrozen)
        {
            isWallSliding = false;
            return;
        }

        if (onWall && !isGrounded && horizontalInput != 0)
            wallCoyoteTimer = coyoteTime;
        else
            wallCoyoteTimer -= Time.fixedDeltaTime;
        
        if (onWall && !isGrounded && wallCoyoteTimer > 0)
        {
            isWallSliding = true;

            if (rb.linearVelocityY > 0f)
            {
                rb.linearVelocityY *= wallSlideUpwardDampening;
            }

            rb.linearVelocityY = Mathf.Clamp(rb.linearVelocityY, -wallSlideSpeed, float.MaxValue);
        }
        else
            isWallSliding = false;
    }

    private void HandleWallJump()
    {
        if (IsMovementFrozen) return;

        if (isWallSliding)
        {
            isWallJumping = false;

            if (!wasWallSliding)
                wallJumpDirection = -FacingDirection;

            wallJumpTimer = wallJumpLeniency;
            CancelInvoke(nameof(StopWallJumping));
        }
        else
            wallJumpTimer -= Time.fixedDeltaTime;

        wasWallSliding = isWallSliding;

        if (jumpPressed && wallJumpTimer > 0f)
        {
            isWallJumping = true;
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(new Vector2(wallJumpDirection * wallJumpForce.x, wallJumpForce.y), ForceMode2D.Impulse);
            wallJumpTimer = 0f;
            jumpPressed = false;
            jumpReleased = false;

            if (FacingDirection != wallJumpDirection)
            {
                FacingDirection *= -1;
                transform.localScale = new Vector3(FacingDirection, transform.localScale.y);
            }
            Invoke(nameof(StopWallJumping), wallJumpDuration);
        }
    }

    private void StopWallJumping()
    {
        isWallJumping = false;
    }

    private void HandleDash()
    {
        if (isDashing)
            dashTimer = dashCoolDown;
        else
            dashTimer -= Time.deltaTime;

        if (dashPressed && isGrounded && dashTimer <= 0f)
        {
            PlayerCombatController combatController = GetComponent<PlayerCombatController>();
            if (combatController != null && combatController.IsParrying)
            {
                combatController.CancelParry();
            }
            else if (IsMovementFrozen)
            {
                return; 
            }

            isDashing = true;
            
            if (horizontalInput != 0)
            {
                IsDirectionalDash = true;
                dashDirection = Mathf.Sign(horizontalInput);
            } 
            else
            {
                IsDirectionalDash = false;
                dashDirection = -FacingDirection;
            }

            var targetSpeed = dashDirection * dashVelocity;
            rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);   

            dashPressed = false;
            dashReleased = false;
            Invoke(nameof(StopDashing), dashDuration);
        }

        if (dashReleased)
        {
            dashPressed = false;
            dashReleased = false;
        }
    }

    private void StopDashing()
    {
        isDashing = false;
    }

    private void HandleSkillCharging()
    {
        if (isChargingSkill)
            chargingSkillTimer += Time.deltaTime;
        
        if (!isChargingSkill && skillPressed && !IsMovementFrozen)
        {
            isChargingSkill = true;
            rb.linearVelocityY = 0;
            skillPressed = false;
            skillReleased = false;

            chargingSkillTimer = 0f;
            Invoke(nameof(StopChargingSkill), chargingSkillMaxDur);
        }
        if (skillReleased && chargingSkillTimer >= chargingSkillMinDur)
        {
            isChargingSkill = false;
            skillPressed = false;
            skillReleased = false;
        }
    }

    private void StopChargingSkill()
    {
        isChargingSkill = false;
    }
    
    private InventoryDisplay inventoryDisplay;

    private void PerformInventoryAction()
    {
        if (!inventoryPressed) return;
        inventoryPressed = false;
        
        if (inventoryDisplay == null)
        {
            inventoryDisplay = Object.FindFirstObjectByType<InventoryDisplay>();
        }

        if (inventoryDisplay != null)
        {
            inventoryDisplay.ToggleInventory();
        }
        else
        {
            Debug.LogWarning("[Player] InventoryDisplay reference missing in scene!");
        }
    }

    private void Flip()
    {
        if (horizontalInput > 0.01f)
            FacingDirection = 1;
        else if (horizontalInput < -0.01f)
            FacingDirection = -1;
        
        transform.localScale = new Vector3(FacingDirection, transform.localScale.y);
    }

    private void GravityState()
    { 
        if (isParryGravityActive || isChargingSkill)
        {
            rb.gravityScale = 0f;
        }
        else if (rb.linearVelocityY > 0.1)
            rb.gravityScale = jumpGravity;
        else if (rb.linearVelocityY < -0.1)
            rb.gravityScale = fallGravity;
        else
            rb.gravityScale = normGravity;
    }

    #region Input System Callbacks

    public void OnMove()
    {
        horizontalInput = moveAction.ReadValue<Vector2>().x;
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            jumpPressed = true;
            jumpReleased = false;
        }
        else
        {
            jumpPressed = false;
            jumpReleased = true;
        }
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed)
        {
            dashPressed = true;
            dashReleased = false;
        }
        else
        {
            dashPressed = false;
            dashReleased = true;
        }
    }

    public void OnSAttack(InputValue value)
    {
        if (value.isPressed)
        {
            skillPressed = true;
            skillReleased = false;
        }
        else
        {
            skillPressed = false;
            skillReleased = true;
        }
    }
    
    public void OnInventory()
    {
        inventoryPressed = true;
    }

    public void OnPause(InputValue value)
    {
        if (value.isPressed && GameManager.Instance != null)
        {
            GameManager.Instance.TogglePause();
        }
    }

    #endregion

    private void GroundCheckUpdate()
    {
        Bounds bounds = GetPlayerBounds();
        
        Vector2 leftFoot = new Vector2(bounds.min.x + edgeMargin, bounds.min.y + 0.02f);
        Vector2 rightFoot = new Vector2(bounds.max.x - edgeMargin, bounds.min.y + 0.02f);
        float rayDistance = groundCheckDistance + 0.04f;

        RaycastHit2D leftHit = Physics2D.Raycast(leftFoot, Vector2.down, rayDistance, groundLayer);
        RaycastHit2D rightHit = Physics2D.Raycast(rightFoot, Vector2.down, rayDistance, groundLayer);

        bool leftOnGround = leftHit.collider != null && leftHit.normal.y > 0.6f;
        bool rightOnGround = rightHit.collider != null && rightHit.normal.y > 0.6f;

        isGrounded = leftOnGround || rightOnGround;

        if (isGrounded && (!leftOnGround || !rightOnGround) && Mathf.Abs(horizontalInput) < 0.01f)
        {
            rb.linearVelocityX = 0f;
        }
    }

    private void WallCheckUpdate()
    {
        onWall = false;

        if (isGrounded) return;
        if (Mathf.Abs(horizontalInput) < 0.1f) return;

        Bounds bounds = GetPlayerBounds();
        float checkDir = Mathf.Sign(horizontalInput);
        float rayLength = bounds.extents.x + wallCheckDistance;

        Vector2 headOrigin  = new Vector2(bounds.center.x, bounds.max.y - (bounds.size.y * 0.10f)); // 90% height
        Vector2 chestOrigin = new Vector2(bounds.center.x, bounds.center.y + (bounds.extents.y * 0.2f)); // 60% height
        Vector2 waistOrigin = new Vector2(bounds.center.x, bounds.min.y + (bounds.size.y * 0.30f)); // 30% height

        RaycastHit2D headHit  = Physics2D.Raycast(headOrigin,  Vector2.right * checkDir, rayLength, groundLayer);
        RaycastHit2D chestHit = Physics2D.Raycast(chestOrigin, Vector2.right * checkDir, rayLength, groundLayer);
        RaycastHit2D waistHit = Physics2D.Raycast(waistOrigin, Vector2.right * checkDir, rayLength, groundLayer);

        if (headHit.collider != null && (chestHit.collider != null || waistHit.collider != null))
        {
            if (Mathf.Abs(headHit.normal.x) > wallCheckNormalThreshold)
            {
                onWall = true;
                lastWallNormal = headHit.normal;
            }
        }
    }

    private bool IsWallTallEnough(RaycastHit2D hit, Bounds playerBounds)
    {
        Vector2 headLevelPoint = new Vector2(hit.point.x + (hit.normal.x * -0.05f), playerBounds.max.y);
        RaycastHit2D heightCheck = Physics2D.Raycast(headLevelPoint, Vector2.down, playerBounds.size.y, groundLayer);
        return heightCheck.collider != null;
    }

    private void OnDrawGizmosSelected()
    {
        Bounds bounds = GetPlayerBounds();
        float checkDir = FacingDirection;
        float rayLength = bounds.extents.x + wallCheckDistance;

        Gizmos.color = onWall ? Color.green : Color.red;

        // Visualizing the full 3-point height check
        Vector2 headOrigin  = new Vector2(bounds.center.x, bounds.max.y - (bounds.size.y * 0.10f));
        Vector2 chestOrigin = new Vector2(bounds.center.x, bounds.center.y + (bounds.extents.y * 0.2f));
        Vector2 waistOrigin = new Vector2(bounds.center.x, bounds.min.y + (bounds.size.y * 0.30f));

        Gizmos.DrawLine(headOrigin,  headOrigin  + Vector2.right * checkDir * rayLength);
        Gizmos.DrawLine(chestOrigin, chestOrigin + Vector2.right * checkDir * rayLength);
        Gizmos.DrawLine(waistOrigin, waistOrigin + Vector2.right * checkDir * rayLength);
    }


    // helper
    private Bounds GetPlayerBounds()
    {
        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponents<Collider2D>();
        }

        if (playerColliders == null || playerColliders.Length == 0) 
            return new Bounds(transform.position, Vector3.one);

        Bounds bounds = playerColliders[0].bounds;
        for (int i = 1; i < playerColliders.Length; i++)
        {
            bounds.Encapsulate(playerColliders[i].bounds);
        }
        return bounds;
    }
}