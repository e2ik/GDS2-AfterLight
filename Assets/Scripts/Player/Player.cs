using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(InteractionManager))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerInventoryManager))]
[RequireComponent(typeof(PlayerEquipmentManager))]
[RequireComponent(typeof(PlayerCombatController))]
[RequireComponent(typeof(PlayerAnimation))]
public class Player : MonoBehaviour
{
    private PlayerInput _input;
    public PlayerInput Input => _input ??= GetComponent<PlayerInput>();

    private InteractionManager _interactionManager;
    public InteractionManager InteractionManager => _interactionManager ??= GetComponent<InteractionManager>();

    private PlayerController _controller;
    public PlayerController Controller => _controller ??= GetComponent<PlayerController>();

    private PlayerStats _stats;
    public PlayerStats Stats => _stats ??= GetComponent<PlayerStats>();

    private PlayerInventoryManager _inventory;
    public PlayerInventoryManager Inventory => _inventory ??= GetComponent<PlayerInventoryManager>();

    private PlayerEquipmentManager _equipment;
    public PlayerEquipmentManager Equipment => _equipment ??= GetComponent<PlayerEquipmentManager>();

    private PlayerCombatController _combatController;
    public PlayerCombatController CombatController => _combatController ??= GetComponent<PlayerCombatController>();

    private PlayerAnimation _animation;
    public PlayerAnimation Animation => _animation ??= GetComponent<PlayerAnimation>();

    private void Start()
    {
        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            uiManager.InitializePlayerUI(gameObject);
        }
        else
        {
            Debug.LogWarning("No UIManager found in the scene. Player UI will not be initialized.");
        }
    }
}