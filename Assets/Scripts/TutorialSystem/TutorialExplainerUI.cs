using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tutorial
{
    public class TutorialExplainerUI : MonoBehaviour
    {
        [SerializeField] private UIWindowAnimator windowAnimator;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private GameObject continueElement;
        [SerializeField] private InputActionReference continueAction;
        [SerializeField] private bool clickElementToContinue = true;
        [SerializeField, Range(0f, 1f)] private float continueWaitingAlpha = 0.4f;
        [Tooltip("The RectTransform with the Content Size Fitter (usually promptLabel's parent panel). Auto-detected if left empty.")]
        [SerializeField] private RectTransform layoutRoot;
        [SerializeField] private InputPromptTextSlots promptSlots;

        [Header("Success Panel")]
        [SerializeField] private GameObject successPanel;
        [SerializeField] private UIWindowAnimator successAnimator;
        [SerializeField] private TMP_Text successLabel;
        [SerializeField] private string[] successMessages = { "Great!", "Nice!", "Well done!", "Perfect!", "You got it!" };
        [SerializeField, Min(0f)] private float successDuration = 1.5f;
        [SerializeField, Min(0f)] private float successHideTime = 0.3f;
        [SerializeField] private bool debugSuccess = false;

        [Header("Success Bounce")]
        [SerializeField] private RectTransform bounceTarget;
        [SerializeField, Min(0f)] private float popDuration = 0.25f;
        [SerializeField, Min(0f)] private float popStartScale = 0.6f;
        [SerializeField] private float popOvershoot = 1.7f;
        [SerializeField] private float bounceHeight = 6f;
        [SerializeField, Min(0f)] private float bouncesPerSecond = 2f;

        [Header("Audio")]
        [SerializeField] private FMODUnity.EventReference promptShowEvent;
        [SerializeField] private FMODUnity.EventReference successEvent;

        private bool subscribed;
        private string rawPromptText;
        private Coroutine successRoutine;
        private int lastSuccessIndex = -1;
        private TutorialStepDefinition shownStep;
        private Coroutine continueDelayRoutine;
        private CanvasGroup continueGroup;
        private bool continueReady;
        private Vector2 bounceBasePosition;
        private Vector3 bounceBaseScale = Vector3.one;

        private void Awake()
        {
            if (continueElement != null)
            {
                continueGroup = continueElement.GetComponent<CanvasGroup>();
                if (continueGroup == null) continueGroup = continueElement.AddComponent<CanvasGroup>();

                if (clickElementToContinue)
                {
                    ContinueClickRelay relay = continueElement.GetComponent<ContinueClickRelay>();
                    if (relay == null) relay = continueElement.AddComponent<ContinueClickRelay>();
                    relay.Clicked += HandleContinuePressed;
                }
            }

            if (layoutRoot == null && promptLabel != null) layoutRoot = promptLabel.transform.parent as RectTransform;
            if (promptSlots == null && promptLabel != null) promptSlots = promptLabel.GetComponent<InputPromptTextSlots>();
            if (successPanel == null && successAnimator != null) successPanel = successAnimator.gameObject;
            if (successPanel == null && successLabel != null && successLabel.transform.parent != null) successPanel = successLabel.transform.parent.gameObject;
            if (successPanel != null && (successPanel == gameObject || transform.IsChildOf(successPanel.transform))) successPanel = null;
            if (bounceTarget == null && successPanel != null) bounceTarget = successPanel.transform as RectTransform;

            if (bounceTarget != null)
            {
                bounceBasePosition = bounceTarget.anchoredPosition;
                bounceBaseScale = bounceTarget.localScale;
            }

            windowAnimator?.InstantHide();
            HideSuccessInstant();
        }

        private void OnEnable()
        {
            TrySubscribe();
            InputManager.OnDeviceChanged += HandleDeviceChanged;
            InputSystem.onActionChange += HandleActionChange;

            if (continueAction != null && continueAction.action != null)
            {
                continueAction.action.performed += HandleContinueAction;
                if (!continueAction.action.enabled) continueAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            InputManager.OnDeviceChanged -= HandleDeviceChanged;
            InputSystem.onActionChange -= HandleActionChange;

            if (continueAction != null && continueAction.action != null)
                continueAction.action.performed -= HandleContinueAction;

            continueReady = false;

            if (successRoutine != null) StopCoroutine(successRoutine);
            successRoutine = null;
            ResetBounce();
            HideSuccessInstant();
        }

        private void Start()
        {
            TrySubscribe();
            if (successRoutine == null) HideSuccessInstant();
        }

        private void TrySubscribe()
        {
            if (subscribed || TutorialDirector.Instance == null) return;
            TutorialDirector.Instance.OnStepBegan += HandleStepBegan;
            TutorialDirector.Instance.OnStepEnded += HandleStepEnded;
            TutorialDirector.Instance.OnStepCompleted += HandleStepCompleted;
            TutorialDirector.Instance.RegisterExcludedWindow(windowAnimator);
            if (successAnimator != null) TutorialDirector.Instance.RegisterExcludedWindow(successAnimator);
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || TutorialDirector.Instance == null) return;
            TutorialDirector.Instance.OnStepBegan -= HandleStepBegan;
            TutorialDirector.Instance.OnStepEnded -= HandleStepEnded;
            TutorialDirector.Instance.OnStepCompleted -= HandleStepCompleted;
            subscribed = false;
        }

        private void HandleStepBegan(TutorialStepDefinition step)
        {
            shownStep = step;
            rawPromptText = step.PromptText;
            ApplyPromptText();

            bool needsContinue = step.ConditionType == TutorialStepConditionType.Prompt;
            if (continueElement != null) continueElement.SetActive(needsContinue);

            if (continueDelayRoutine != null) StopCoroutine(continueDelayRoutine);
            continueDelayRoutine = null;
            SetContinueReady(needsContinue);

            if (needsContinue && step.StartDelay > 0f && isActiveAndEnabled)
                continueDelayRoutine = StartCoroutine(ContinueDelayRoutine(step.StartDelay));

            windowAnimator?.Show(freezeplayer: false);
            AudioManager.PlaySFX(promptShowEvent, this);
        }

        private IEnumerator ContinueDelayRoutine(float delay)
        {
            SetContinueReady(false);
            yield return new WaitForSeconds(delay);
            SetContinueReady(shownStep != null && shownStep.ConditionType == TutorialStepConditionType.Prompt);
            continueDelayRoutine = null;
        }

        private void SetContinueReady(bool ready)
        {
            continueReady = ready;
            if (continueGroup != null) continueGroup.alpha = ready ? 1f : continueWaitingAlpha;
        }

        private void HandleContinueAction(InputAction.CallbackContext context) => HandleContinuePressed();

        private void HandleContinuePressed()
        {
            if (!continueReady) return;

            continueReady = false;
            TutorialDirector.Instance?.NotifyPlayerContinued();
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
            shownStep = null;
            continueReady = false;
            if (continueDelayRoutine != null) StopCoroutine(continueDelayRoutine);
            continueDelayRoutine = null;
            windowAnimator?.Hide();
        }

        private void HandleStepCompleted(TutorialStepDefinition step)
        {
            if (step == null || step != shownStep) return;
            if (!step.ShowSuccessOnComplete) return;

            if (debugSuccess) Debug.Log($"[TutorialExplainerUI] Success shown for step '{step.PromptText}'", this);
            ShowSuccess();
        }

        private void ShowSuccess()
        {
            if (successLabel == null && successAnimator == null && successPanel == null) return;
            if (!isActiveAndEnabled) return;

            if (successLabel != null) successLabel.text = PickSuccessMessage();

            AudioManager.PlaySFX(successEvent, this);

            if (successRoutine != null) StopCoroutine(successRoutine);
            ResetBounce();
            successRoutine = StartCoroutine(SuccessRoutine());
        }

        private IEnumerator SuccessRoutine()
        {
            if (successPanel != null && !successPanel.activeSelf) successPanel.SetActive(true);
            if (successLabel != null && !successLabel.gameObject.activeSelf) successLabel.gameObject.SetActive(true);
            if (successAnimator != null) successAnimator.Show(freezeplayer: false);

            float elapsed = 0f;
            while (elapsed < successDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyBounce(elapsed);
                yield return null;
            }

            ResetBounce();

            if (successAnimator != null)
            {
                successAnimator.Hide();
                if (successHideTime > 0f) yield return new WaitForSecondsRealtime(successHideTime);
            }

            if (successPanel != null) successPanel.SetActive(false);

            successRoutine = null;
        }

        private void ApplyBounce(float elapsed)
        {
            if (bounceTarget == null) return;

            float scale = 1f;
            if (popDuration > 0f && elapsed < popDuration)
            {
                float t = Mathf.Clamp01(elapsed / popDuration) - 1f;
                float back = 1f + t * t * ((popOvershoot + 1f) * t + popOvershoot);
                scale = Mathf.LerpUnclamped(popStartScale, 1f, back);
            }

            bounceTarget.localScale = bounceBaseScale * scale;

            float bob = Mathf.Abs(Mathf.Sin(elapsed * bouncesPerSecond * Mathf.PI)) * bounceHeight;
            bounceTarget.anchoredPosition = bounceBasePosition + new Vector2(0f, bob);
        }

        private void ResetBounce()
        {
            if (bounceTarget == null) return;
            bounceTarget.localScale = bounceBaseScale;
            bounceTarget.anchoredPosition = bounceBasePosition;
        }

        private void HideSuccessInstant()
        {
            if (successAnimator != null) successAnimator.InstantHide();
            if (successPanel != null) successPanel.SetActive(false);
        }

        private string PickSuccessMessage()
        {
            if (successMessages == null || successMessages.Length == 0) return "Great!";
            if (successMessages.Length == 1) return successMessages[0];

            int index;
            do
            {
                index = Random.Range(0, successMessages.Length);
            } while (index == lastSuccessIndex);

            lastSuccessIndex = index;
            return successMessages[index];
        }

    }

    public class ContinueClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public event System.Action Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }
}