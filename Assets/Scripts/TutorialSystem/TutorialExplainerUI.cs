using TMPro;
using UnityEngine;
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

        private bool subscribed;

        private void Awake()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(HandleContinueClicked);

            if (layoutRoot == null && promptLabel != null) layoutRoot = promptLabel.transform.parent as RectTransform;

            windowAnimator?.InstantHide();
        }

        private void OnEnable() => TrySubscribe();
        private void OnDisable() => Unsubscribe();

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
            if (promptLabel != null) promptLabel.text = step.PromptText;
            if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            bool needsContinueButton = step.ConditionType == TutorialStepConditionType.Prompt;
            if (continueButton != null) continueButton.gameObject.SetActive(needsContinueButton);

            windowAnimator?.Show(freezeplayer: false);
        }

        private void HandleStepEnded(TutorialStepDefinition step) => windowAnimator?.Hide();

        private void HandleContinueClicked() => TutorialDirector.Instance?.NotifyPlayerContinued();
    }
}