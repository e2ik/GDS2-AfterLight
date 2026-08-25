using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction attackAction;
    private InputAction sAttackAction;
    private float horizontalInput;

    public bool MovementEnabled { get; set; } = true;
    public int FacingDirection { get; private set; } = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        PlayerInput playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        attackAction = playerInput.actions["Attack"];
        sAttackAction = playerInput.actions["SAttack"];
    }

    private void Update()
    {
        horizontalInput = MovementEnabled ? moveAction.ReadValue<Vector2>().x : 0f;

        if (horizontalInput > 0.01f)
            FacingDirection = 1;
        else if (horizontalInput < -0.01f)
            FacingDirection = -1;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
    }
}