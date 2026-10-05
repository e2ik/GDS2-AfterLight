using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameUI
{
    [System.Serializable]
    public class RebindEntry
    {
        public InputActionReference action;
        public string compositePart;
        public string displayLabel;
        [Tooltip("Only show this entry on the Keyboard tab (e.g. Move, which uses the stick on gamepad).")]
        public bool keyboardOnly;
        public bool allowSharedBinding = true;
    }

    public class RebindListBuilder : MonoBehaviour
    {
        [SerializeField] private RebindRow rowPrefab;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private CanvasGroup sharedWaitingPrompt;
        [SerializeField] private List<RebindEntry> entries;
        [SerializeField] private bool startInGamepadMode;

        private bool currentlyGamepad;

        public static event System.Action OnBindingsReset;

        public static void NotifyBindingsReset() => OnBindingsReset?.Invoke();

        public InputActionAsset ActionAsset
        {
            get
            {
                if (entries == null) return null;
                foreach (RebindEntry entry in entries)
                {
                    if (entry != null && entry.action != null && entry.action.action != null)
                        return entry.action.action.actionMap.asset;
                }
                return null;
            }
        }

        private void Awake()
        {
            OnBindingsReset += HandleBindingsReset;
            PopulateRows(useGamepad: startInGamepadMode);
        }

        private void OnDestroy()
        {
            OnBindingsReset -= HandleBindingsReset;
        }

        private void HandleBindingsReset() => PopulateRows(currentlyGamepad);

        public void ShowKeyboard() => PopulateRows(useGamepad: false);
        public void ShowGamepad() => PopulateRows(useGamepad: true);

        private void PopulateRows(bool useGamepad)
        {
            currentlyGamepad = useGamepad;

            foreach (Transform child in rowContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (RebindEntry entry in entries)
            {
                if (entry == null || entry.action == null || entry.action.action == null) continue;
                if (useGamepad && entry.keyboardOnly) continue;

                int index = FindBindingIndex(entry, useGamepad ? "Gamepad" : "Keyboard");
                if (index < 0)
                {
                    Debug.LogWarning($"[RebindListBuilder] No {(useGamepad ? "gamepad" : "keyboard")} binding found on '{entry.action.action.name}'{(string.IsNullOrEmpty(entry.compositePart) ? "" : $" for part '{entry.compositePart}'")}.");
                    continue;
                }

                RebindRow row = Instantiate(rowPrefab, rowContainer);
                row.gameObject.SetActive(false);
                row.RebindButton.Initialize(
                    entry.action, index, entry.displayLabel, sharedWaitingPrompt,
                    useGamepad ? RebindButton.RebindDevice.Gamepad : RebindButton.RebindDevice.Keyboard);
                row.RebindButton.AllowSharedBinding = entry.allowSharedBinding;
                row.gameObject.SetActive(true);
            }
        }

        private static int FindBindingIndex(RebindEntry entry, string layoutName)
        {
            InputAction action = entry.action.action;
            bool wantPart = !string.IsNullOrEmpty(entry.compositePart);

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite) continue;

                if (wantPart)
                {
                    if (!binding.isPartOfComposite) continue;
                    if (!string.Equals(binding.name, entry.compositePart, System.StringComparison.OrdinalIgnoreCase)) continue;
                }
                else if (binding.isPartOfComposite)
                {
                    continue;
                }

                string layout = InputControlPath.TryGetDeviceLayout(binding.path);
                if (string.IsNullOrEmpty(layout)) continue;
                if (InputSystem.IsFirstLayoutBasedOnSecond(layout, layoutName)) return i;
            }

            return -1;
        }
    }
}