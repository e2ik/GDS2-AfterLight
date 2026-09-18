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

        private bool subscribed;

        private void Awake()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(HandleContinueClicked);

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

            bool needsContinueButton = step.ConditionType == TutorialStepConditionType.Prompt;
            if (continueButton != null) continueButton.gameObject.SetActive(needsContinueButton);

            windowAnimator?.Show(freezeplayer: false);
        }

        private void HandleStepEnded(TutorialStepDefinition step) => windowAnimator?.Hide();

        private void HandleContinueClicked() => TutorialDirector.Instance?.NotifyPlayerContinued();
    }
}