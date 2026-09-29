using UnityEngine;

[CreateAssetMenu(fileName = "InputIconDatabase", menuName = "UI/Input Icon Database")]
public class InputIconDatabase : ScriptableObject
{
    [System.Serializable]
    public class GamepadIcons
    {
        public Sprite buttonSouth;
        public Sprite buttonEast;
        public Sprite buttonWest;
        public Sprite buttonNorth;
        public Sprite leftShoulder;
        public Sprite rightShoulder;
        public Sprite leftTrigger;
        public Sprite rightTrigger;
        public Sprite start;
        public Sprite select;
        public Sprite leftStick;
        public Sprite rightStick;
        public Sprite leftStickPress;
        public Sprite rightStickPress;
        public Sprite dpad;
        public Sprite dpadUp;
        public Sprite dpadDown;
        public Sprite dpadLeft;
        public Sprite dpadRight;

        public Sprite Get(string control)
        {
            switch (control)
            {
                case "buttonSouth": return buttonSouth;
                case "buttonEast": return buttonEast;
                case "buttonWest": return buttonWest;
                case "buttonNorth": return buttonNorth;
                case "leftShoulder": return leftShoulder;
                case "rightShoulder": return rightShoulder;
                case "leftTrigger": return leftTrigger;
                case "rightTrigger": return rightTrigger;
                case "start": return start;
                case "select": return select;
                case "leftStick": return leftStick;
                case "rightStick": return rightStick;
                case "leftStickPress": return leftStickPress;
                case "rightStickPress": return rightStickPress;
                case "dpad": return dpad;
                case "dpad/up": return dpadUp != null ? dpadUp : dpad;
                case "dpad/down": return dpadDown != null ? dpadDown : dpad;
                case "dpad/left": return dpadLeft != null ? dpadLeft : dpad;
                case "dpad/right": return dpadRight != null ? dpadRight : dpad;
                default: return null;
            }
        }
    }

    [Header("Actions")]
    [SerializeField] private UnityEngine.InputSystem.InputActionAsset actions;
    public UnityEngine.InputSystem.InputActionAsset Actions => actions;

    [Header("Gamepads")]
    [SerializeField] private GamepadIcons xbox = new GamepadIcons();
    [SerializeField] private GamepadIcons playStation = new GamepadIcons();
    [SerializeField] private GamepadIcons nintendoSwitch = new GamepadIcons();
    [SerializeField] private GamepadIcons genericGamepad = new GamepadIcons();

    [System.Serializable]
    public class KeyLabelOverride
    {
        public UnityEngine.InputSystem.Key key;
        public string label;
    }

    [Header("Keyboard")]
    [SerializeField] private Sprite keycap;
    [SerializeField] private Sprite wideKeycap;
    [SerializeField] private int wideKeyMinLength = 2;
    [SerializeField] private KeyLabelOverride[] keyLabelOverrides = new KeyLabelOverride[0];

    [Header("Mouse")]
    [SerializeField] private Sprite mouseLeft;
    [SerializeField] private Sprite mouseRight;
    [SerializeField] private Sprite mouseMiddle;

    public Sprite GetGamepadSprite(InputDeviceType device, string control)
    {
        GamepadIcons set = device switch
        {
            InputDeviceType.PlayStation => playStation,
            InputDeviceType.Switch => nintendoSwitch,
            InputDeviceType.Xbox => xbox,
            _ => genericGamepad
        };

        Sprite sprite = set?.Get(control);
        if (sprite == null) sprite = genericGamepad?.Get(control);
        if (sprite == null) sprite = xbox?.Get(control);
        return sprite;
    }

    public Sprite GetMouseSprite(string control)
    {
        switch (control)
        {
            case "leftButton": return mouseLeft;
            case "rightButton": return mouseRight;
            case "middleButton": return mouseMiddle;
            default: return null;
        }
    }

    public string GetKeyLabel(string controlName, string displayName)
    {
        if (!string.IsNullOrEmpty(controlName))
        {
            foreach (KeyLabelOverride entry in keyLabelOverrides)
            {
                if (entry == null || entry.key == UnityEngine.InputSystem.Key.None) continue;

                if (string.Equals(KeyControlName(entry.key), controlName, System.StringComparison.OrdinalIgnoreCase))
                    return entry.label ?? string.Empty;
            }
        }

        return displayName ?? string.Empty;
    }

    private static string KeyControlName(UnityEngine.InputSystem.Key key)
    {
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null) return keyboard[key].name;

        string name = key.ToString();
        if (name.StartsWith("Digit") && name.Length == 6) return name.Substring(5);
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    public Sprite GetKeycap(string label)
    {
        bool wide = label != null && label.Length >= wideKeyMinLength && wideKeycap != null;
        return wide ? wideKeycap : keycap;
    }
}