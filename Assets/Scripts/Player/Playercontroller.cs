using System;
using UnityEngine;
using UnityEngine.InputSystem;
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

    private bool isWallJumping;
    private float wallJumpDirection;
    private float wallJumpTime = 0.2f;
    private float wallJumpCoolDown;
    private float wallJumpDuration = 0.4f;
    private Vector2 wallJumpPower = new(8f, 16f);
    [SerializeField] private float wallJumpVelocity = 5f;
    

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
        if (!isWallJumping)
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
        
        GravityState();
    }

    private void HandleMovement()
    {
        if (!isWallJumping)
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
            Debug.Log(rb.linearVelocityY);
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
            wallJumpCoolDown = wallJumpTime;
            
            CancelInvoke(nameof(StopWallJumping));
        }
        else
        {
            wallJumpCoolDown -= Time.deltaTime;
        }

        if (jumpPressed && wallJumpCoolDown > 0f)
        {
            isWallJumping = true;
            rb.linearVelocity = new Vector2(wallJumpDirection * wallJumpPower.x, wallJumpPower.y);
            wallJumpCoolDown = 0f;

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