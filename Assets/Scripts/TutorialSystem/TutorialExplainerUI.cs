using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tutorial
{
    public class TutorialExplainerUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private Button continueButton;

        private void Awake()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(HandleContinueClicked);

            SetPanelVisible(false);
        }

        private void OnEnable() => TrySubscribe();
        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            TrySubscribe();
        }

        private bool subscribed;

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

            SetPanelVisible(true);
        }

        private void HandleStepEnded(TutorialStepDefinition step) => SetPanelVisible(false);

        private void HandleContinueClicked() => TutorialDirector.Instance?.NotifyPlayerContinued();

        private void SetPanelVisible(bool visible)
        {
            if (panelRoot != null) panelRoot.SetActive(visible);
        }
    }
}