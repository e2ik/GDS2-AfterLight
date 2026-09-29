using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tutorial
{
    public class TutorialExplainerUI : MonoBehaviour
    {
        [SerializeField] private UIWindowAnimator windowAnimator;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private Button continueButton;
        [Tooltip("The RectTransform with the Content Size Fitter (usually promptLabel's parent panel). Auto-detected if left empty.")]
        [SerializeField] private RectTransform layoutRoot;
        [SerializeField] private InputPromptTextSlots promptSlots;

        private bool subscribed;
        private string rawPromptText;

        private void Awake()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(HandleContinueClicked);

            if (layoutRoot == null && promptLabel != null) layoutRoot = promptLabel.transform.parent as RectTransform;
            if (promptSlots == null && promptLabel != null) promptSlots = promptLabel.GetComponent<InputPromptTextSlots>();

            windowAnimator?.InstantHide();
        }

        private void OnEnable()
        {
            TrySubscribe();
            InputManager.OnDeviceChanged += HandleDeviceChanged;
            InputSystem.onActionChange += HandleActionChange;
        }

        private void OnDisable()
        {
            Unsubscribe();
            InputManager.OnDeviceChanged -= HandleDeviceChanged;
            InputSystem.onActionChange -= HandleActionChange;
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void TrySubscribe()
        {
            if (subscribed || TutorialDirector.Instance == null) return;
            TutorialDirector.Instance.OnStepBegan += HandleStepBegan;
            TutorialDirector.Instance.OnStepEnded += HandleStepEnded;
            TutorialDirector.Instance.RegisterExcludedWindow(windowAnimator);
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || TutorialDirector.Instance == null) return;
            TutorialDirector.Instance.OnStepBegan -= HandleStepBegan;
            TutorialDirector.Instance.OnStepEnded -= HandleStepEnded;
            subscribed = false;
        }

        private void HandleStepBegan(TutorialStepDefinition step)
        {
            rawPromptText = step.PromptText;
            ApplyPromptText();

            bool needsContinueButton = step.ConditionType == TutorialStepConditionType.Prompt;
            if (continueButton != null) continueButton.gameObject.SetActive(needsContinueButton);

            windowAnimator?.Show(freezeplayer: false);
        }

        private void ApplyPromptText()
        {
            if (promptSlots != null)
            {
                promptSlots.SetText(rawPromptText);
                if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
                promptSlots.RebuildPrompts();
                return;
            }

            if (promptLabel != null) promptLabel.text = rawPromptText;
            if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
        }

        private void HandleDeviceChanged(InputDeviceType device)
        {
            if (!string.IsNullOrEmpty(rawPromptText)) ApplyPromptText();
        }

        private void HandleActionChange(object obj, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged && !string.IsNullOrEmpty(rawPromptText))
                ApplyPromptText();
        }

        private void HandleStepEnded(TutorialStepDefinition step)
        {
            rawPromptText = null;
            windowAnimator?.Hide();
        }

        private void HandleContinueClicked() => TutorialDirector.Instance?.NotifyPlayerContinued();
    }
}