using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private CanvasGroup waitingForInputPrompt;
        [SerializeField] private string unboundLabel = "-";

        public Button Button => rebindButton;

        private static readonly List<RebindButton> activeButtons = new List<RebindButton>();

        private RebindingOperation rebindingOperation;
        private string previousOverridePath;

        private void Awake()
        {
            rebindButton.onClick.AddListener(StartRebind);
        }

        private void OnEnable()
        {
            if (!activeButtons.Contains(this)) activeButtons.Add(this);
            RefreshDisplay();
        }

        private void OnDisable()
        {
            activeButtons.Remove(this);
            rebindingOperation?.Cancel();
        }

        public void Initialize(InputActionReference action, int newBindingIndex, string label, CanvasGroup sharedWaitingPrompt)
        {
            actionReference = action;
            bindingIndex = newBindingIndex;
            waitingForInputPrompt = sharedWaitingPrompt;
            if (actionNameText != null) { actionNameText.text = label; }
        }

        public void RefreshDisplay()
        {
            if (actionReference == null || actionReference.action == null) { return; }

            string display = actionReference.action.GetBindingDisplayString(
                bindingIndex,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);

            bindingDisplayText.text = string.IsNullOrEmpty(display) ? unboundLabel : display;
        }

        private void StartRebind()
        {
            rebindButton.interactable = false;
            SetPromptVisible(true);
            InputAction action = actionReference.action;
            previousOverridePath = action.bindings[bindingIndex].overridePath;
            action.Disable();
            UIManager.Instance.SuppressCancel = true;
            rebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsHavingToMatchPath("<Keyboard>")
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

            if (FindConflict(newPath, out InputAction conflictAction, out int conflictIndex))
            {
                RebindButton conflictButton = FindButtonFor(conflictAction, conflictIndex);

                if (conflictButton != null)
                {
                    conflictAction.ApplyBindingOverride(conflictIndex, string.Empty);
                    conflictButton.RefreshDisplay();
                    Debug.Log($"[RebindButton] '{newPath}' moved from '{conflictAction.name}' to '{actionReference.action.name}'; '{conflictAction.name}' is now unbound.");
                }
                else
                {
                    RestorePreviousBinding();
                    Debug.LogWarning($"[RebindButton] '{newPath}' is used by '{conflictAction.name}', which isn't in the rebind list. Rebind reverted.");
                }
            }

            FinishCleanup();
        }

        private void OnRebindCancelled()
        {
            rebindingOperation.Dispose();
            FinishCleanup();
        }

        private void RestorePreviousBinding()
        {
            if (previousOverridePath == null)
                actionReference.action.RemoveBindingOverride(bindingIndex);
            else
                actionReference.action.ApplyBindingOverride(bindingIndex, previousOverridePath);
        }

        private void FinishCleanup()
        {
            actionReference.action.Enable();
            rebindButton.interactable = true;
            SetPromptVisible(false);
            RefreshDisplay();
            InputRebindSaver.Save(actionReference.action.actionMap.asset);
            EventSystem.current?.SetSelectedGameObject(rebindButton.gameObject);
            UIManager.Instance.SuppressCancel = false;
        }

        private void SetPromptVisible(bool visible)
        {
            if (waitingForInputPrompt == null) { return; }
            waitingForInputPrompt.alpha = visible ? 1f : 0f;
            waitingForInputPrompt.interactable = visible;
            waitingForInputPrompt.blocksRaycasts = visible;
        }

        private bool FindConflict(string newPath, out InputAction conflictAction, out int conflictIndex)
        {
            conflictAction = null;
            conflictIndex = -1;
            if (string.IsNullOrEmpty(newPath)) return false;

            InputActionMap map = actionReference.action.actionMap;
            foreach (InputAction otherAction in map.actions)
            {
                for (int i = 0; i < otherAction.bindings.Count; i++)
                {
                    InputBinding binding = otherAction.bindings[i];
                    if (binding.isComposite) { continue; }
                    if (otherAction == actionReference.action && i == bindingIndex) { continue; }
                    if (binding.effectivePath == newPath)
                    {
                        conflictAction = otherAction;
                        conflictIndex = i;
                        return true;
                    }
                }
            }
            return false;
        }

        private static RebindButton FindButtonFor(InputAction action, int index)
        {
            foreach (RebindButton button in activeButtons)
            {
                if (button == null || button.actionReference == null) continue;
                if (button.actionReference.action == action && button.bindingIndex == index) return button;
            }
            return null;
        }

        public void CancelIfRebinding()
        {
            rebindingOperation?.Cancel();
        }
    }
}