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
        GameUI.PlayerHUD hud = FindFirstObjectByType<GameUI.PlayerHUD>();
        if (hud != null)
        {
            hud.Bind(Stats, CombatController, Equipment);
        }
        else
        {
            Debug.LogWarning("No PlayerHUD found in the scene. HUD will not be initialized.");
        }

        GameUI.InteractionPopup interactionPopup = FindFirstObjectByType<GameUI.InteractionPopup>();
        if (interactionPopup != null)
        {
            interactionPopup.Bind(InteractionManager);
        }
        else
        {
            Debug.LogWarning("No InteractionPopup found in the scene. Interaction icon will not be initialized.");
        }
    }
}