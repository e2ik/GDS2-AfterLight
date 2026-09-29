using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameUI
{
    [System.Serializable]
    public class RebindEntry
    {
        public InputActionReference action;
        public string compositePart;
        public string displayLabel;
    }

    public class RebindListBuilder : MonoBehaviour
    {
        [SerializeField] private RebindRow rowPrefab;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private CanvasGroup sharedWaitingPrompt;
        [SerializeField] private List<RebindEntry> entries;

        private void Awake()
        {
            PopulateRows();
        }

        private void PopulateRows()
        {
            foreach (Transform child in rowContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (RebindEntry entry in entries)
            {
                if (entry == null || entry.action == null || entry.action.action == null) continue;

                int index = FindKeyboardBindingIndex(entry);
                if (index < 0)
                {
                    Debug.LogWarning($"[RebindListBuilder] No keyboard binding found on '{entry.action.action.name}'{(string.IsNullOrEmpty(entry.compositePart) ? "" : $" for part '{entry.compositePart}'")}.");
                    continue;
                }

                RebindRow row = Instantiate(rowPrefab, rowContainer);
                row.gameObject.SetActive(false);
                row.RebindButton.Initialize(entry.action, index, entry.displayLabel, sharedWaitingPrompt);
                row.gameObject.SetActive(true);
            }
        }

        private static int FindKeyboardBindingIndex(RebindEntry entry)
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
                if (InputSystem.IsFirstLayoutBasedOnSecond(layout, "Keyboard")) return i;
            }

            return -1;
        }
    }
}