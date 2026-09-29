using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class InputModeTracker : MonoBehaviour
{
    private const float MouseMoveThreshold = 1f;
    private const float StickThreshold = 0.5f;

    public static bool IsUsingMouse { get; private set; } = true;

    public static void ForceNonMouse() => IsUsingMouse = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject go = new GameObject("InputModeTracker");
        DontDestroyOnLoad(go);
        go.AddComponent<InputModeTracker>();
    }

    private void Update()
    {
        if (UsedMouse()) IsUsingMouse = true;
        else if (UsedKeyboardOrGamepad()) IsUsingMouse = false;
    }

    private static bool UsedMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return false;

        return mouse.delta.ReadValue().sqrMagnitude > MouseMoveThreshold
            || mouse.leftButton.wasPressedThisFrame
            || mouse.rightButton.wasPressedThisFrame
            || mouse.scroll.ReadValue().sqrMagnitude > 0f;
    }

    private static bool UsedKeyboardOrGamepad()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

        Gamepad pad = Gamepad.current;
        if (pad == null) return false;

        if (pad.leftStick.ReadValue().sqrMagnitude > StickThreshold * StickThreshold) return true;

        foreach (InputControl control in pad.allControls)
        {
            if (control is ButtonControl button && button.wasPressedThisFrame) return true;
        }

        return false;
    }
}