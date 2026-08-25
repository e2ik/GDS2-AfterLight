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

    public WeaponDefinition weapon;
    public PrimaryGemBehaviourDefinition specialAttackDef;
    public SecondaryGemInstance secondaryGem;

    public bool MovementEnabled { get; set; } = true;
    public int FacingDirection { get; private set; } = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        PlayerInput playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        attackAction = playerInput.actions["Attack"];
        sAttackAction = playerInput.actions["SAttack"];
        ClearSecondaryGem();
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
            Debug.Log("Attack");
        }
        if (sAttackAction.WasPressedThisFrame())
        {
            Debug.Log("S Attack");
            AttackContext newAttack = new AttackContext
            {
                BaseAttackDamage = weapon.BaseWeaponDamage,
                BaseAttackCrit = weapon.BaseWeaponCrit,
            };
            SpecialAttack(newAttack, secondaryGem, specialAttackDef);
        }
    }

    private void FixedUpdate()
    {
        float verticalVelocity = MovementEnabled ? rb.linearVelocity.y : 0f;
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, verticalVelocity);
    }

    private void SpecialAttack(AttackContext context, SecondaryGemInstance modifier, PrimaryGemBehaviourDefinition attackStrategy)
    {
        if(modifier.InstTemplateID != null)
        {
            GameDatabase.GetSecondaryTemplateFromID(modifier.InstTemplateID).Modify(ref context,modifier);
        }
        attackStrategy.Execute(context);
    }

    public void ClearSecondaryGem()
    {
        secondaryGem.InstTemplateID = null;
        secondaryGem.InstanceGUID = null;
        secondaryGem.InstDamageMult = 0;
        secondaryGem.InstCritMult = 0;
        secondaryGem.InstSizeMult = 0;
    }
}