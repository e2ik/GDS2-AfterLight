using UnityEngine;

public class DeviceVisibility : MonoBehaviour
{
    public enum ShowWhen { Gamepad, KeyboardMouse }

    [SerializeField] private ShowWhen showWhen = ShowWhen.Gamepad;
    [SerializeField] private GameObject[] targets;

    private void OnEnable()
    {
        InputManager.OnDeviceChanged += HandleDeviceChanged;
        Apply();
    }

    private void OnDisable()
    {
        InputManager.OnDeviceChanged -= HandleDeviceChanged;
    }

    private void HandleDeviceChanged(InputDeviceType device) => Apply();

    private void Apply()
    {
        bool usingGamepad = InputManager.IsUsingGamepad;
        bool visible = showWhen == ShowWhen.Gamepad ? usingGamepad : !usingGamepad;

        if (targets == null) return;

        foreach (GameObject target in targets)
        {
            if (target != null && target != gameObject && target.activeSelf != visible)
                target.SetActive(visible);
        }
    }
}