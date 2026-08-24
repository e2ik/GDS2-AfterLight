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

    [SerializeField] private float wallJumpVelocity = 5f;
    [SerializeField] private float wallSlideGravity;

    [Header("Ground Check")] 
    [SerializeField] private Transform groundCheck;
    public LayerMask groundLayer;
    private bool isGrounded;
    [SerializeField] private float groundCheckRadius;
    
    [Header("Wall Check")] 
    [SerializeField] private Transform rightWallCheck; 
    [SerializeField] private Transform leftWallCheck;
    public LayerMask wallLayer;
    private bool isWallSliding;
    private bool isRightWallSliding;
    private bool isLeftWallSliding;
    [SerializeField] private float wallCheckRadius;
    
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
        Flip();
    }

    private void FixedUpdate()
    {
        GroundCheckUpdate();
        WallCheckUpdate();
        GravityState();
        HandleMovement();
        HandleJump();
    }

    private void HandleMovement()
    {
        if (isGrounded) //can move on ground
        {
            targetSpeed = MovementEnabled ? horizontalInput * moveSpeed : 0f;
        }
        else if (isWallSliding)
        {
            //have to hold down left or right to stay on wall
            //if let go, fall
        }
        else //cannot move while falling
        {
            targetSpeed = rb.linearVelocityX;
        }
        
        rb.linearVelocity = new Vector2(targetSpeed, rb.linearVelocity.y);
    }

    private void HandleJump()
    {
        if (jumpPressed && isGrounded)
        {
            rb.linearVelocityY = jumpVelocity;
            jumpPressed = false;
            jumpReleased = false;
        } 
        else if (jumpPressed && isWallSliding)
        {
            if (isRightWallSliding)
            {
                rb.linearVelocityY = wallJumpVelocity;
                rb.AddForce(new Vector2(-wallJumpVelocity, 0));
            }
            else if (isLeftWallSliding)
            {
                rb.linearVelocityY = wallJumpVelocity;
                rb.AddForce(new Vector2(wallJumpVelocity, 0));
            }
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

    private void Flip()
    {
        if (horizontalInput > 0.01f)
            FacingDirection = 1;
        else if (horizontalInput < -0.01f)
            FacingDirection = -1;
    }
    
    private void GroundCheckUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void WallCheckUpdate()
    {
        isRightWallSliding = Physics2D.OverlapCircle(rightWallCheck.position, wallCheckRadius, wallLayer);
        isLeftWallSliding = Physics2D.OverlapCircle(leftWallCheck.position, wallCheckRadius, wallLayer);
        isWallSliding = isRightWallSliding || isLeftWallSliding;
    }

    private void GravityState()
    {
        if (isWallSliding && rb.linearVelocityY < -0.1)
            rb.gravityScale = wallSlideGravity;
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
            jumpReleased = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        Gizmos.DrawWireSphere(leftWallCheck.position, groundCheckRadius);
        Gizmos.DrawWireSphere(rightWallCheck.position, groundCheckRadius);
    }
}