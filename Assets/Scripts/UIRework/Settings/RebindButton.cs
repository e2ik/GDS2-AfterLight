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

        public Button Button => rebindButton;

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

        public void Initialize(InputActionReference action, int newBindingIndex, string label, CanvasGroup sharedWaitingPrompt)
        {
            actionReference = action;
            bindingIndex = newBindingIndex;
            waitingForInputPrompt = sharedWaitingPrompt;
            if (actionNameText != null) { actionNameText.text = label; }
        }

        private void RefreshDisplay()
        {
            if (actionReference == null || actionReference.action == null) { return; }

            bindingDisplayText.text = actionReference.action.GetBindingDisplayString(
                bindingIndex,
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        private void StartRebind()
        {
            rebindButton.interactable = false;
            SetPromptVisible(true);
            InputAction action = actionReference.action;
            action.Disable();
            UIManager.Instance.SuppressCancel = true;
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