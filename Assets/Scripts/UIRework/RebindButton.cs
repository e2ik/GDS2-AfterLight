using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputActionRebindingExtensions;

namespace GameUI
{
    public class RebindButton : MonoBehaviour
    {
        [SerializeField] private InputActionReference actionReference;
        [SerializeField] private int bindingIndex = 0;

        [SerializeField] private TMP_Text actionNameText;
        [SerializeField] private TMP_Text bindingDisplayText;
        [SerializeField] private Button rebindButton;
        [SerializeField] private GameObject waitingForInputPrompt;

        private RebindingOperation rebindingOperation;

        private void Awake()
        {
            rebindButton.onClick.AddListener(StartRebind);
        }

        private void OnEnable()
        {
            RefreshDisplay();
        }

        private void OnDisable()
        {
            rebindingOperation?.Cancel();
        }

        private void RefreshDisplay()
        {
            if (actionNameText != null) { actionNameText.text = actionReference.action.name; }

            bindingDisplayText.text = actionReference.action.GetBindingDisplayString(
                bindingIndex,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        private void StartRebind()
        {
            rebindButton.interactable = false;
            waitingForInputPrompt.SetActive(true);

            InputAction action = actionReference.action;
            action.Disable();

            rebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(operation => OnRebindComplete())
                .OnCancel(operation => OnRebindCancelled())
                .Start();
        }

        private void OnRebindComplete()
        {
            rebindingOperation.Dispose();

            string newPath = actionReference.action.bindings[bindingIndex].effectivePath;

            if (IsDuplicateElsewhere(newPath))
            {
                actionReference.action.RemoveBindingOverride(bindingIndex);
                Debug.LogWarning($"[RebindButton] '{newPath}' is already bound to another action on this map. Rebind reverted.");
            }

            FinishCleanup();
        }

        private void OnRebindCancelled()
        {
            rebindingOperation.Dispose();
            FinishCleanup();
        }

        private void FinishCleanup()
        {
            actionReference.action.Enable();

            rebindButton.interactable = true;
            waitingForInputPrompt.SetActive(false);

            RefreshDisplay();
            InputRebindSaver.Save(actionReference.action.actionMap.asset);
        }

        private bool IsDuplicateElsewhere(string newPath)
        {
            InputActionMap map = actionReference.action.actionMap;

            foreach (InputAction otherAction in map.actions)
            {
                for (int i = 0; i < otherAction.bindings.Count; i++)
                {
                    InputBinding binding = otherAction.bindings[i];

                    if (binding.isComposite) { continue; }
                    if (otherAction == actionReference.action && i == bindingIndex) { continue; }

                    if (binding.effectivePath == newPath) { return true; }
                }
            }

            return false;
        }
    }
}