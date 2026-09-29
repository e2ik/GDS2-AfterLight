using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InputActionPrompt : MonoBehaviour
{
    [SerializeField] private InputActionReference action;
    [SerializeField] private InputIconDatabase icons;
    [SerializeField] private string compositePart;

    private InputAction runtimeAction;
    private InputAction CurrentAction => runtimeAction ?? (action != null ? action.action : null);
    private InputIconDatabase Icons => icons != null ? icons : InputManager.Icons;

    [Header("Display")]
    [SerializeField] private Image iconImage;
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField] private bool upperCaseKeys = true;

    private void OnEnable()
    {
        InputManager.OnDeviceChanged += HandleDeviceChanged;
        InputSystem.onActionChange += HandleActionChange;
        Refresh();
    }

    private void OnDisable()
    {
        InputManager.OnDeviceChanged -= HandleDeviceChanged;
        InputSystem.onActionChange -= HandleActionChange;
    }

    public void SetAction(InputActionReference newAction)
    {
        action = newAction;
        runtimeAction = null;
        Refresh();
    }

    public void SetAction(InputAction newAction, string part = null)
    {
        runtimeAction = newAction;
        compositePart = part;
        Refresh();
    }

    private void HandleDeviceChanged(InputDeviceType device) => Refresh();

    private void HandleActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged) Refresh();
    }

    public void Refresh()
    {
        InputAction inputAction = CurrentAction;
        InputIconDatabase icons = Icons;
        if (inputAction == null || icons == null) return;

        InputDeviceType device = InputManager.CurrentDevice;
        bool wantGamepad = device != InputDeviceType.KeyboardMouse;

        if (!InputBindingUtility.TryFindBinding(inputAction, wantGamepad, compositePart, out int bindingIndex, out string control, out bool isMouse))
        {
            Show(null, string.Empty);
            return;
        }

        string display = inputAction.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);

        if (wantGamepad)
        {
            Sprite sprite = icons.GetGamepadSprite(device, control);
            Show(sprite, sprite != null ? string.Empty : display);
            return;
        }

        if (isMouse)
        {
            Sprite mouseSprite = icons.GetMouseSprite(control);
            if (mouseSprite != null)
            {
                Show(mouseSprite, string.Empty);
                return;
            }
        }

        string label = icons.GetKeyLabel(control, display);
        if (upperCaseKeys) label = label.ToUpperInvariant();
        Show(icons.GetKeycap(label), label);
    }

    private void Show(Sprite sprite, string label)
    {
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        if (iconRenderer != null)
            iconRenderer.sprite = sprite;

        if (keyLabel != null)
        {
            keyLabel.text = label;
            keyLabel.enabled = !string.IsNullOrEmpty(label);
        }
    }
}