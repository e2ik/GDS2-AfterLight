using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(InteractionManager))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerInventoryManager))]
[RequireComponent(typeof(PlayerEquipmentManager))]
public class Player : MonoBehaviour
{
    public PlayerInput Input { get; private set; }
    public InteractionManager InteractionManager { get; private set; }
    public PlayerController Controller { get; private set; }
    public PlayerStats Stats { get; private set; }
    public PlayerInventoryManager Inventory { get; private set; }
    public PlayerEquipmentManager Equipment { get; private set; }

    private void Awake()
    {
        Input = GetComponent<PlayerInput>();
        InteractionManager = GetComponent<InteractionManager>();
        Controller = GetComponent<PlayerController>();
        Stats = GetComponent<PlayerStats>();
        Inventory = GetComponent<PlayerInventoryManager>();
        Equipment = GetComponent<PlayerEquipmentManager>();
    }
}