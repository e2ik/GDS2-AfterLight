using UnityEngine.InputSystem;

public static class InputBindingUtility
{
    public static bool TryFindBinding(InputAction action, bool wantGamepad, string partName,
        out int bindingIndex, out string control, out bool isMouse)
    {
        bindingIndex = -1;
        control = null;
        isMouse = false;
        if (action == null) return false;

        bool wantPart = !string.IsNullOrEmpty(partName);
        var bindings = action.bindings;

        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];

            if (wantPart)
            {
                if (!binding.isPartOfComposite) continue;
                if (!string.Equals(binding.name, partName, System.StringComparison.OrdinalIgnoreCase)) continue;
            }
            else if (binding.isPartOfComposite)
            {
                continue;
            }

            string path = binding.isComposite && i + 1 < bindings.Count
                ? bindings[i + 1].effectivePath
                : binding.effectivePath;

            if (string.IsNullOrEmpty(path)) continue;

            string layout = InputControlPath.TryGetDeviceLayout(path);
            if (string.IsNullOrEmpty(layout)) continue;

            bool bindingIsGamepad = InputSystem.IsFirstLayoutBasedOnSecond(layout, "Gamepad");
            bool bindingIsMouse = InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse");
            bool bindingIsKeyboard = InputSystem.IsFirstLayoutBasedOnSecond(layout, "Keyboard");

            if (wantGamepad ? !bindingIsGamepad : !(bindingIsKeyboard || bindingIsMouse)) continue;

            bindingIndex = i;
            isMouse = bindingIsMouse;
            control = binding.isComposite ? string.Empty : ControlName(path);
            return true;
        }

        return false;
    }

    public static string ControlName(string path)
    {
        int slash = path.IndexOf('/');
        return slash >= 0 && slash + 1 < path.Length ? path.Substring(slash + 1) : path;
    }
}