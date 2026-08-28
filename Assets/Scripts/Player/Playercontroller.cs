using System;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UI;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

[RequireComponent(typeof(Rigidbody2D))]
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

    [Header("Ground & Wall Check")] 
    [SerializeField] private Transform groundCheck;
    public LayerMask groundLayer;
    private bool isGrounded;
    private float groundCheckRadius = 0.2f;
    
    [SerializeField] private Transform wallCheck; 
    public LayerMask wallLayer;
    private bool onWall;
    private float wallCheckRadius = 0.2f;
    
    private Rigidbody2D rb;
    private PlayerEquipmentManager equipmentManager;

    private InputAction moveAction;
    
    private InputAction attackAction;
    private InputAction sAttackAction;
    private InputAction inventoryAction;
    private float horizontalInput;
    private Vector2 jumpInput;

    public bool MovementEnabled { get; set; } = true;    
    public int FacingDirection { get; private set; } = 1;

    // anim required
    public bool IsGrounded => isGrounded;
    public bool IsWallSliding => isWallSliding;
    public bool IsDashing => isDashing;
    public bool IsChargingSkill => isChargingSkill;
    public bool IsDirectionalDash { get; private set; }


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
        if (!MovementEnabled) return;

        PerformInventoryAction();
        HandleInteract();
    }

    private void FixedUpdate()
    {
        GroundCheckUpdate();
        WallCheckUpdate();

        HandleMovement();
        HandleJump();
        
        HandleWallSlide();
        HandleWallJump();
        HandleDash();
        
        GravityState();
    }
    
    private bool CanMove()
    {
        return MovementEnabled && !isWallJumping && !isDashing && !isChargingSkill;
    }
    
    private void HandleMovement()
    {
        if (!CanMove()) return;
        
        float targetSpeed = horizontalInput * moveSpeed;
        float speedDif = targetSpeed - rb.linearVelocityX;
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
        float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, velocityPower) * Mathf.Sign(speedDif);
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
        if (isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.fixedDeltaTime;
        
        if (jumpPressed && coyoteTimeCounter > 0f)
        {
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

    private void HandleWallSlide()
    {
        if (onWall && !isGrounded && horizontalInput != 0)
            wallCoyoteTimer = coyoteTime;
        else
            wallCoyoteTimer -= Time.deltaTime;
        
        if (onWall && !isGrounded && wallCoyoteTimer > 0)
        {
            isWallSliding = true;
            rb.linearVelocityY = Mathf.Clamp(rb.linearVelocityY, -wallSlideSpeed, float.MaxValue);
        }
        else
            isWallSliding = false;
    }
    private void HandleWallJump()
    {
        if (isWallSliding)
        {
            isWallJumping = false;
            wallJumpDirection = -FacingDirection;
            wallJumpTimer = wallJumpLeniency;
            
            CancelInvoke(nameof(StopWallJumping));
        }
        else
            wallJumpTimer -= Time.deltaTime;

        if (jumpPressed && wallJumpTimer > 0f)
        {
            isWallJumping = true;
            //rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpForce.x, wallJumpForce.y);
            rb.AddForce(new Vector2(wallJumpDirection * wallJumpForce.x, wallJumpForce.y), ForceMode2D.Impulse);
            wallJumpTimer = 0f;

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
            isDashing = true;
            
            if (horizontalInput != 0)
            {
                IsDirectionalDash = true; // Directional input
                dashDirection = Mathf.Sign(horizontalInput);
            } 
            else
            {
                IsDirectionalDash = false; // Neutral input
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
        
        if (!isChargingSkill && skillPressed) // && is not charged && is enabled by skill
        {
            isChargingSkill = true;
            rb.linearVelocityY = 0;
            skillPressed = false;
            skillReleased = false;

            chargingSkillTimer = 0f;
            Invoke(nameof(StopChargingSkill), chargingSkillMaxDur); //get from skills points needed to charge
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

    private void HandleInteract()
    {
        if (!interactPressed) return;
        interactPressed = false;
        
        //interact logic
        //if it doesn't work, will have to check the input interactions in controls
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
        if (isChargingSkill)
        {
            rb.gravityScale = 0;
        }
        else if (rb.linearVelocityY > 0.1)
            rb.gravityScale = jumpGravity;
        else if (rb.linearVelocityY < -0.1)
            rb.gravityScale = fallGravity;
        else
            rb.gravityScale = normGravity;
    }

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

    public void OnInteract()
    {
        interactPressed = true;
    }
    public void OnInventory()
    {
        inventoryPressed = true;
    }
    
    private void GroundCheckUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void WallCheckUpdate()
    {
        onWall = Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, wallLayer);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        Gizmos.DrawWireSphere(wallCheck.position, wallCheckRadius);
    }
}