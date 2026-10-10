using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UISelectionKeeper : MonoBehaviour
{
    [SerializeField] private bool onlyWhenMenuOpen = true;
    [SerializeField] private float stickThreshold = 0.5f;
    [SerializeField] private bool restrictToTopWindow = true;
    [SerializeField] private bool logEscapedSelections = false;

    private GameObject lastValidSelection;
    private bool stickWasHeld;
    private Vector2 lastNavDirection;
    private int lastNavFrame = -100;

    private void Update()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject current = eventSystem.currentSelectedGameObject;
        bool navigationPressed = NavigationPressed();

        bool recentNavigation = Time.frameCount - lastNavFrame <= 2;
        bool escaped = recentNavigation && current != null && current != lastValidSelection && EscapedTopWindow(current);

        if (IsValid(current) && !escaped)
        {
            lastValidSelection = current;
            return;
        }

        if (escaped && IsValid(lastValidSelection))
        {
            if (logEscapedSelections)
                Debug.Log($"[UISelectionKeeper] Navigation escaped to '{GetPath(current.transform)}', redirecting.", current);

            GameObject redirected = FindInDirection(lastValidSelection, lastNavDirection);

            eventSystem.SetSelectedGameObject(redirected != null ? redirected : lastValidSelection);
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

    private bool EscapedTopWindow(GameObject go)
    {
        if (!restrictToTopWindow || go == null || lastValidSelection == null) return false;

        GameUI.UIManager manager = GameUI.UIManager.Instance;
        if (manager == null || !manager.HasOpenWindows) return false;

        GameUI.UIWindow previousWindow = lastValidSelection.GetComponentInParent<GameUI.UIWindow>();
        if (previousWindow == null || !manager.IsTopmost(previousWindow)) return false;

        GameUI.UIWindow window = go.GetComponentInParent<GameUI.UIWindow>();
        return window != previousWindow;
    }

    private static GameObject FindInDirection(GameObject origin, Vector2 direction)
    {
        if (origin == null || direction == Vector2.zero) return null;

        GameUI.UIWindow window = origin.GetComponentInParent<GameUI.UIWindow>();
        if (window == null) return null;

        Vector3 originPos = GetCenter(origin.transform);
        Vector3 dir = new Vector3(direction.x, direction.y, 0f).normalized;
        Selectable best = null;
        float bestScore = float.NegativeInfinity;

        foreach (Selectable candidate in window.GetComponentsInChildren<Selectable>())
        {
            if (candidate == null || candidate.gameObject == origin) continue;
            if (!candidate.IsInteractable() || candidate.navigation.mode == Navigation.Mode.None) continue;

            Vector3 offset = GetCenter(candidate.transform) - originPos;
            float dot = Vector3.Dot(dir, offset);
            if (dot <= 0f) continue;

            float score = dot / offset.sqrMagnitude;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best != null ? best.gameObject : null;
    }

    private static Vector3 GetCenter(Transform t)
    {
        if (t is RectTransform rect) return rect.TransformPoint(rect.rect.center);
        return t.position;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static bool IsMenuOpen()
    {
        GameUI.UIManager manager = GameUI.UIManager.Instance;
        return manager == null || manager.HasOpenWindows;
    }

    private bool NavigationPressed()
    {
        bool pressed = false;
        Vector2 direction = Vector2.zero;

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            bool stickHeld = stick.magnitude >= stickThreshold;
            if (stickHeld && !stickWasHeld)
            {
                pressed = true;
                direction = Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2(Mathf.Sign(stick.x), 0f) : new Vector2(0f, Mathf.Sign(stick.y));
            }
            stickWasHeld = stickHeld;

            if (gamepad.dpad.up.wasPressedThisFrame) direction = Vector2.up;
            if (gamepad.dpad.down.wasPressedThisFrame) direction = Vector2.down;
            if (gamepad.dpad.left.wasPressedThisFrame) direction = Vector2.left;
            if (gamepad.dpad.right.wasPressedThisFrame) direction = Vector2.right;

            if (gamepad.dpad.up.wasPressedThisFrame || gamepad.dpad.down.wasPressedThisFrame
                || gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame
                || gamepad.buttonSouth.wasPressedThisFrame)
                pressed = true;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) direction = Vector2.up;
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) direction = Vector2.down;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) direction = Vector2.left;
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) direction = Vector2.right;

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame
                || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame
                || keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame
                || keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame
                || keyboard.tabKey.wasPressedThisFrame)
                pressed = true;
        }

        if (direction != Vector2.zero)
        {
            lastNavDirection = direction;
            lastNavFrame = Time.frameCount;
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