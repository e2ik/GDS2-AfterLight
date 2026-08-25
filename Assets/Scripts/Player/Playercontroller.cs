using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpVelocity = 20f;
    [SerializeField] private float tapJumpMultiplier = 0.5f;
    [SerializeField] private float normGravity;
    [SerializeField] private float jumpGravity;
    [SerializeField] private float fallGravity;

    [Header("Wall Movement")] 
    private bool onWall;
    private bool isWallSliding;
    private float wallSlideSpeed = 2f;

    [SerializeField] private float wallJumpVelocity = 5f;
    private bool isWallJumping;
    private float wallJumpDirection;
    private float wallJumpTime = 0.2f;
    private float wallJumpLeniency;
    private float wallJumpDuration = 0.4f;
    private Vector2 wallJumpPower = new(8f, 16f);
    
    [Header("Dash Movement")]
    [SerializeField] private float dashVelocity = 20f;
    private float dashDuration = 0.2f;
    private bool dashPressed;
    private bool dashReleased;
    private bool isDashing;
    private float dashDirection;
    private float dashCoolDown = 0.2f;
    private float dashTimer;

    [Header("Ground Check")] 
    [SerializeField] private Transform groundCheck;
    public LayerMask groundLayer;
    private bool isGrounded;
    private float groundCheckRadius = 0.2f;
    
    [Header("Wall Check")] 
    [SerializeField] private Transform wallCheck; 
    public LayerMask wallLayer;
    private float wallCheckRadius = 0.2f;
    
    private Rigidbody2D rb;
    private InputAction moveAction;
    private float targetSpeed;
    private bool jumpPressed;
    private bool jumpReleased;
    
    
    private float horizontalInput;
    private Vector2 jumpInput;

    public bool MovementEnabled { get; set; } = true;
    
    public int FacingDirection { get; private set; } = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        PlayerInput playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
    }

    private void Start()
    {
        rb.gravityScale = normGravity;
    }

    private void Update()
    {
        if (!isWallJumping && !isDashing)
        {
            Flip();
        }
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

    private void HandleMovement()
    {
        if (!isWallJumping && !isDashing)
        {
            targetSpeed = MovementEnabled ? horizontalInput * moveSpeed : 0f;
            rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);   
        }
    }

    private void HandleJump()
    {
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocityY = jumpVelocity;
            jumpPressed = false;
            jumpReleased = false;
        }
        if (jumpReleased)
        {
            if (rb.linearVelocityY > 0)
            {
                rb.linearVelocityY *= tapJumpMultiplier;
            }
            jumpReleased = false;
        }
    }

    private void HandleWallSlide()
    {
        if (onWall && !isGrounded && horizontalInput != 0)
        {
            isWallSliding = true;
            rb.linearVelocityY = Mathf.Clamp(rb.linearVelocityY, -wallSlideSpeed, float.MaxValue);
        }
        else
        {
            isWallSliding = false;
        }
    }
    private void HandleWallJump()
    {
        if (isWallSliding)
        {
            isWallJumping = false;
            wallJumpDirection = -FacingDirection;
            wallJumpLeniency = wallJumpTime;
            
            CancelInvoke(nameof(StopWallJumping));
        }
        else
        {
            wallJumpLeniency -= Time.deltaTime;
        }

        if (jumpPressed && wallJumpLeniency > 0f)
        {
            isWallJumping = true;
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpPower.x, wallJumpPower.y);
            wallJumpLeniency = 0f;

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
        {
            dashTimer = dashCoolDown;
        }
        else
        {
            dashTimer -= Time.deltaTime;
        }

        if (dashPressed && isGrounded && dashTimer <= 0f)
        {
            isDashing = true;
            if (horizontalInput != 0)
            {
                //dash forward
                targetSpeed = horizontalInput * dashVelocity;
                rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);   
                
            } else
            {
                //dash backwards
                targetSpeed = -FacingDirection * dashVelocity;
                rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);   
            }
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
        if (rb.linearVelocityY > 0.1)
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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        Gizmos.DrawWireSphere(wallCheck.position, wallCheckRadius);
    }
}