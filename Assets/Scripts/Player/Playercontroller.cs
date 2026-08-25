using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private PlayerEquipmentManager equipmentManager;

    private InputAction moveAction;
    private InputAction attackAction;
    private InputAction sAttackAction;

    private float horizontalInput;

    public bool MovementEnabled { get; set; } = true;
    public int FacingDirection { get; private set; } = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        equipmentManager = GetComponent<PlayerEquipmentManager>();

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

        if (!MovementEnabled) return;

        if (attackAction.WasPressedThisFrame())
        {
            PerformPrimaryAttack();
        }

        if (sAttackAction.WasPressedThisFrame())
        {
            PerformSpecialAttack();
        }
    }

    private void FixedUpdate()
    {
        float verticalVelocity = MovementEnabled ? rb.linearVelocity.y : 0f;
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, verticalVelocity);
    }

    private void PerformPrimaryAttack()
    {
        if (equipmentManager.EquippedWeapon == null)
        {
            Debug.LogWarning("[PlayerController] Cannot attack: No weapon equipped in PlayerEquipmentManager.");
            return;
        }

        Debug.Log("Primary Attack executed.");
    }

    private void PerformSpecialAttack()
    {
        PrimaryGemBehaviourDefinition specialDef = equipmentManager.SpecialAttackDef;

        if (specialDef == null)
        {
            Debug.LogWarning("[PlayerController] Cannot execute Special Attack: No Primary Gem equipped.");
            return;
        }

        AttackContext context = equipmentManager.GetModifiedAttackContext();

        specialDef.Execute(context);
    }
}