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
        public enum RebindDevice { Keyboard, Gamepad }

        private RebindDevice device = RebindDevice.Keyboard;

        [SerializeField] private InputActionReference actionReference;
        [SerializeField] private int bindingIndex = 0;

        [SerializeField] private TMP_Text actionNameText;
        [SerializeField] private TMP_Text bindingDisplayText;
        [SerializeField] private Button rebindButton;
        [SerializeField] private CanvasGroup waitingForInputPrompt;
        [SerializeField] private string unboundLabel = "-";
        [SerializeField] private Image bindingIcon;
        [SerializeField] private bool allowSharedBinding = true;
        [SerializeField] private string waitingLabel = "??";
        [SerializeField] private TMP_Text buttonLabelText;
        [SerializeField] private string rebindingButtonLabel = "...";

        public Button Button => rebindButton;

        public bool AllowSharedBinding
        {
            get => allowSharedBinding;
            set => allowSharedBinding = value;
        }

        private static readonly List<RebindButton> activeButtons = new List<RebindButton>();
        private static int rebindingCount;
        private static int lastRebindFinishFrame = -10;
        public static bool AnyRebinding => rebindingCount > 0 || Time.frameCount - lastRebindFinishFrame <= 1;
        private bool isRebinding;
        public bool IsRebinding => isRebinding;

        private RebindingOperation rebindingOperation;
        private string defaultButtonLabel;

        private void Awake()
        {
            rebindButton.onClick.AddListener(StartRebind);
            if (buttonLabelText == null && rebindButton != null) buttonLabelText = rebindButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonLabelText != null) defaultButtonLabel = buttonLabelText.text;
        }

        private void OnEnable()
        {
            if (!activeButtons.Contains(this)) activeButtons.Add(this);
            InputManager.OnDeviceChanged += HandleDeviceChanged;
            RefreshDisplay();
        }

        private void OnDisable()
        {
            activeButtons.Remove(this);
            InputManager.OnDeviceChanged -= HandleDeviceChanged;
            rebindingOperation?.Cancel();
        }

        private void HandleDeviceChanged(InputDeviceType _) => RefreshDisplay();

        public void Initialize(InputActionReference action, int newBindingIndex, string label, CanvasGroup sharedWaitingPrompt, RebindDevice rebindDevice)
        {
            actionReference = action;
            bindingIndex = newBindingIndex;
            waitingForInputPrompt = sharedWaitingPrompt;
            device = rebindDevice;
            if (actionNameText != null) { actionNameText.text = label; }
        }

        public void RefreshDisplay()
        {
            if (actionReference == null || actionReference.action == null) { return; }

            Sprite sprite = null;

            if (device == RebindDevice.Gamepad)
            {
                InputIconDatabase icons = InputManager.Icons;
                string path = actionReference.action.bindings[bindingIndex].effectivePath;

                if (icons != null && !string.IsNullOrEmpty(path))
                {
                    string control = InputBindingUtility.ControlName(path);
                    sprite = icons.GetGamepadSprite(InputManager.ResolveGamepadIconDevice(), control);
                }
            }

            if (bindingIcon != null)
            {
                bindingIcon.sprite = sprite;
                bindingIcon.enabled = sprite != null;
            }

            if (bindingDisplayText != null)
            {
                bool showText = sprite == null;
                if (showText)
                {
                    string display = actionReference.action.GetBindingDisplayString(
                        bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                    bindingDisplayText.text = string.IsNullOrEmpty(display) ? unboundLabel : display;
                }
                bindingDisplayText.enabled = showText;
            }
        }

        private void StartRebind()
        {
            if (isRebinding) return;

            isRebinding = true;
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
            if (buttonLabelText != null) buttonLabelText.text = rebindingButtonLabel;
            rebindingCount++;
            SetPromptVisible(true);
            ShowWaitingLabel();
            InputAction action = actionReference.action;
            action.Disable();
            UIManager.Instance.SuppressCancel = true;

            RebindingOperation operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape");

            if (device == RebindDevice.Gamepad)
            {
                operation = operation
                    .WithControlsHavingToMatchPath("<Gamepad>")
                    .WithCancelingThrough("<Gamepad>/start");
            }
            else
            {
                operation = operation
                    .WithControlsHavingToMatchPath("<Keyboard>")
                    .WithControlsExcluding("Mouse");
            }

            rebindingOperation = operation
                .OnComplete(_ => OnRebindComplete())
                .OnCancel(_ => OnRebindCancelled())
                .Start();
        }

        private void OnRebindComplete()
        {
            rebindingOperation.Dispose();

            if (!allowSharedBinding)
                ClearConflictingBindings(actionReference.action.bindings[bindingIndex].effectivePath);

            FinishCleanup();
        }

        private void ClearConflictingBindings(string newPath)
        {
            if (string.IsNullOrEmpty(newPath)) return;

            InputActionMap map = actionReference.action.actionMap;
            foreach (InputAction otherAction in map.actions)
            {
                for (int i = 0; i < otherAction.bindings.Count; i++)
                {
                    InputBinding binding = otherAction.bindings[i];
                    if (binding.isComposite) continue;
                    if (otherAction == actionReference.action && i == bindingIndex) continue;
                    if (binding.effectivePath != newPath) continue;

                    RebindButton otherButton = FindButtonFor(otherAction, i);
                    if (otherButton == null) continue;

                    otherAction.ApplyBindingOverride(i, string.Empty);
                    otherButton.RefreshDisplay();
                    Debug.Log($"[RebindButton] '{newPath}' moved from '{otherAction.name}' to '{actionReference.action.name}'; '{otherAction.name}' is now unbound.");
                }
            }
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

        private void OnRebindCancelled()
        {
            rebindingOperation.Dispose();
            FinishCleanup();
        }

        private void FinishCleanup()
        {
            if (isRebinding)
            {
                isRebinding = false;
                rebindingCount = Mathf.Max(0, rebindingCount - 1);
                lastRebindFinishFrame = Time.frameCount;
            }

            actionReference.action.Enable();
            if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
            if (buttonLabelText != null) buttonLabelText.text = defaultButtonLabel;
            SetPromptVisible(false);
            RefreshDisplay();
            InputRebindSaver.Save(actionReference.action.actionMap.asset);
            EventSystem.current?.SetSelectedGameObject(rebindButton.gameObject);
            UIManager.Instance.SuppressCancel = false;
        }

        private void ShowWaitingLabel()
        {
            if (bindingIcon != null) bindingIcon.enabled = false;

            if (bindingDisplayText != null)
            {
                bindingDisplayText.text = waitingLabel;
                bindingDisplayText.enabled = true;
            }
        }

        private void SetPromptVisible(bool visible)
        {
            if (waitingForInputPrompt == null) { return; }
            waitingForInputPrompt.alpha = visible ? 1f : 0f;
            waitingForInputPrompt.interactable = visible;
            waitingForInputPrompt.blocksRaycasts = visible;
        }

        public void CancelIfRebinding()
        {
            rebindingOperation?.Cancel();
        }
    }
}