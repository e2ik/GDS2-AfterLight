using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UISelectionKeeper : MonoBehaviour
{
    [SerializeField] private bool onlyWhenMenuOpen = true;
    [SerializeField] private float stickThreshold = 0.5f;

    private GameObject lastValidSelection;
    private bool stickWasHeld;

    private void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject current = eventSystem.currentSelectedGameObject;
        bool navigationPressed = NavigationPressed();

        if (IsValid(current))
        {
            lastValidSelection = current;
            return;
        }

        if (!navigationPressed) return;
        if (onlyWhenMenuOpen && !IsMenuOpen()) return;

        GameObject target = IsValid(lastValidSelection) ? lastValidSelection : FindBestSelectable();
        if (target != null)
            eventSystem.SetSelectedGameObject(target);
    }

    private static bool IsValid(GameObject go)
    {
        if (go == null || !go.activeInHierarchy) return false;
        if (!go.TryGetComponent(out Selectable selectable)) return true;
        return selectable.IsInteractable();
    }

    private static bool IsMenuOpen()
    {
        GameUI.UIManager manager = GameUI.UIManager.Instance;
        return manager == null || manager.HasOpenWindows;
    }

    private bool NavigationPressed()
    {
        bool pressed = false;

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            bool stickHeld = gamepad.leftStick.ReadValue().magnitude >= stickThreshold;
            if (stickHeld && !stickWasHeld) pressed = true;
            stickWasHeld = stickHeld;

            if (gamepad.dpad.up.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame
                || gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame
                || gamepad.buttonSouth.wasPressedThisFrame)
                pressed = true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                || keyboard.tabKey.wasPressedThisFrame)
                pressed = true;
        }

        return pressed;
    }

    private static GameObject FindBestSelectable()
    {
        Selectable best = null;

        foreach (Selectable selectable in Selectable.allSelectablesArray)
        {
            if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.IsInteractable()) continue;
            if (selectable.navigation.mode == Navigation.Mode.None) continue;

            if (best == null || IsBetterCandidate(selectable.transform.position, best.transform.position))
                best = selectable;
        }

        return best != null ? best.gameObject : null;
    }

    private static bool IsBetterCandidate(Vector3 candidate, Vector3 current)
    {
        if (!Mathf.Approximately(candidate.y, current.y))
            return candidate.y > current.y;

        return candidate.x < current.x;
    }
}