using UnityEngine;
using UnityEngine.InputSystem;

public enum ParryDirection
{
    Up,
    Down,
    Left,
    Right
}

public class PlayerCombatController : MonoBehaviour
{
    [Header("Parry Settings")]
    // how long parry is active (to actually parry attacks
    [SerializeField] private float parryActiveDuration = 0.2f;

    // recovery window of when you can parry again ^ e.g. for 0.1 after active parry player is vulnerable
    [SerializeField] private float parryRecoveryDuration = 0.3f;

    // actual input buffer
    [SerializeField] private float parryBufferTime = 0.15f;

    private float parryActiveTimer;
    private float parryRecoveryTimer;
    private float parryBufferTimer;

    private bool isParrying;
    private bool isParryInRecovery;
    private ParryDirection parryDir;

    [Header("Other Combat References")]
    [SerializeField] private Transform attackOrigin;
    public LayerMask enemyLayer;

    private float verticalInput;
    private PlayerController pController;
    private PlayerEquipmentManager equipmentManager;

    public bool IsParrying => isParrying;
    public bool IsParryInRecovery => isParryInRecovery;

    private void Awake()
    {
        pController = GetComponent<PlayerController>();
        equipmentManager = GetComponent<PlayerEquipmentManager>();
    }

    private void Update()
    {
        if (!pController.MovementEnabled) return;

        UpdateTimers();
        HandleParryLogic();
    }

    private void UpdateTimers()
    {
        if (parryBufferTimer > 0f)
            parryBufferTimer -= Time.deltaTime;

        if (isParrying)
        {
            parryActiveTimer -= Time.deltaTime;
            if (parryActiveTimer <= 0f)
            {
                isParrying = false;
                isParryInRecovery = true;
                parryRecoveryTimer = parryRecoveryDuration;
            }
        }

        if (isParryInRecovery)
        {
            parryRecoveryTimer -= Time.deltaTime;
            if (parryRecoveryTimer <= 0f)
            {
                isParryInRecovery = false;
            }
        }
    }

    private void HandleParryLogic()
    {
        if (parryBufferTimer > 0f && CanParry())
        {
            ExecuteParry();
        }
    }

    private bool CanParry()
    {
        return pController.MovementEnabled 
            && !pController.IsWallSliding 
            && !pController.IsChargingSkill 
            && !isParrying 
            && !isParryInRecovery;
    }

    private void ExecuteParry()
    {
        parryBufferTimer = 0f;

        isParrying = true;
        isParryInRecovery = false;
        parryActiveTimer = parryActiveDuration;

        parryDir = pController.FacingDirection == 1 ? ParryDirection.Right : ParryDirection.Left;

        if (verticalInput > 0.01f)
        {
            parryDir = ParryDirection.Up;
        }
        else if (verticalInput < -0.01f && !pController.IsGrounded)
        {
            parryDir = ParryDirection.Down;
        }

        Debug.Log($"Parry Executed! Direction: {parryDir}");
    }

    public bool CheckParry(ParryDirection incomingDirection)
    {
        if (isParrying && parryDir == incomingDirection)
        {
            OnSuccessfulParry();
            return true;
        }
        return false;
    }

    public void OnSuccessfulParry()
    {
        isParrying = false;
        isParryInRecovery = false;
        parryActiveTimer = 0f;
        parryRecoveryTimer = 0f;

        Debug.Log("PARRY SUCCESSFUL!");
    }

    public void OnParry()
    {
        parryBufferTimer = parryBufferTime;
    }

    public void OnMove(InputValue value)
    {
        verticalInput = value.Get<Vector2>().y;
    }
}