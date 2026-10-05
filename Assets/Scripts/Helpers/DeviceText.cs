using TMPro;
using UnityEngine;

public class DeviceText : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private string gamepadText = "L1 <";
    [SerializeField] private string keyboardText = "Q <";

    private void Awake()
    {
        if (text == null) text = GetComponentInChildren<TMP_Text>(true);
    }

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
        if (text == null) return;
        text.text = InputManager.IsUsingGamepad ? gamepadText : keyboardText;
    }
}