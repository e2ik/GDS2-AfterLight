using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;

public enum InputDeviceType
{
    KeyboardMouse,
    Xbox,
    PlayStation,
    Switch,
    Gamepad
}

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    public static InputDeviceType CurrentDevice { get; private set; } = InputDeviceType.KeyboardMouse;
    public static Gamepad CurrentGamepad { get; private set; }
    public static bool IsUsingGamepad => CurrentDevice != InputDeviceType.KeyboardMouse;

    public static event Action<InputDeviceType> OnDeviceChanged;

    [SerializeField] private InputIconDatabase iconDatabase;
    public static InputIconDatabase Icons => Instance != null ? Instance.iconDatabase : null;

    [SerializeField] private float stickThreshold = 0.5f;
    [SerializeField] private float triggerThreshold = 0.5f;
    [SerializeField] private float mouseMoveThreshold = 3f;
    [SerializeField] private bool hideCursorOnGamepad = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);

        ApplyCursor();
    }

    private void OnEnable() => InputSystem.onDeviceChange += HandleDeviceChange;
    private void OnDisable() => InputSystem.onDeviceChange -= HandleDeviceChange;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (!IsGamepadActive(gamepad)) continue;

            CurrentGamepad = gamepad;
            SetDevice(Classify(gamepad));
            return;
        }

        if (IsKeyboardMouseActive())
            SetDevice(InputDeviceType.KeyboardMouse);
    }

    private bool IsGamepadActive(Gamepad gamepad)
    {
        if (gamepad == null) return false;

        if (gamepad.leftStick.ReadValue().magnitude >= stickThreshold) return true;
        if (gamepad.rightStick.ReadValue().magnitude >= stickThreshold) return true;
        if (gamepad.leftTrigger.ReadValue() >= triggerThreshold) return true;
        if (gamepad.rightTrigger.ReadValue() >= triggerThreshold) return true;

        return gamepad.buttonSouth.wasPressedThisFrame
            || gamepad.buttonEast.wasPressedThisFrame
            || gamepad.buttonWest.wasPressedThisFrame
            || gamepad.buttonNorth.wasPressedThisFrame
            || gamepad.leftShoulder.wasPressedThisFrame
            || gamepad.rightShoulder.wasPressedThisFrame
            || gamepad.startButton.wasPressedThisFrame
            || gamepad.selectButton.wasPressedThisFrame
            || gamepad.leftStickButton.wasPressedThisFrame
            || gamepad.rightStickButton.wasPressedThisFrame
            || gamepad.dpad.up.wasPressedThisFrame
            || gamepad.dpad.down.wasPressedThisFrame
            || gamepad.dpad.left.wasPressedThisFrame
            || gamepad.dpad.right.wasPressedThisFrame;
    }

    private bool IsKeyboardMouseActive()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

        Mouse mouse = Mouse.current;
        if (mouse == null) return false;

        return mouse.leftButton.wasPressedThisFrame
            || mouse.rightButton.wasPressedThisFrame
            || mouse.middleButton.wasPressedThisFrame
            || mouse.scroll.ReadValue().sqrMagnitude > 0.01f
            || mouse.delta.ReadValue().magnitude >= mouseMoveThreshold;
    }

    public static InputDeviceType Classify(Gamepad gamepad)
    {
        if (gamepad == null) return InputDeviceType.Gamepad;
        if (gamepad is DualShockGamepad) return InputDeviceType.PlayStation;
        if (gamepad is XInputController) return InputDeviceType.Xbox;

        string description = $"{gamepad.description.manufacturer} {gamepad.description.product} {gamepad.name}".ToLowerInvariant();

        if (description.Contains("sony") || description.Contains("playstation")
            || description.Contains("dualshock") || description.Contains("dualsense"))
            return InputDeviceType.PlayStation;

        if (description.Contains("xbox") || description.Contains("xinput") || description.Contains("microsoft"))
            return InputDeviceType.Xbox;

        if (description.Contains("nintendo") || description.Contains("switch") || description.Contains("pro controller"))
            return InputDeviceType.Switch;

        return InputDeviceType.Gamepad;
    }

    private void SetDevice(InputDeviceType device)
    {
        if (device == CurrentDevice) return;

        CurrentDevice = device;
        ApplyCursor();
        OnDeviceChanged?.Invoke(device);
    }

    private void ApplyCursor()
    {
        if (!hideCursorOnGamepad) return;
        Cursor.visible = !IsUsingGamepad;
    }

    private void HandleDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected) return;
        if (device != CurrentGamepad) return;

        CurrentGamepad = null;
        SetDevice(InputDeviceType.KeyboardMouse);
    }
}