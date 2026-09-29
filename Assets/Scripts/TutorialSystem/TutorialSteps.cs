using System;
using Enemies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tutorial
{
    #region Step Definition & Evaluator Contract

    public enum TutorialStepConditionType
    {
        Prompt,
        TimedText,
        ButtonPress,
        DefeatEnemies,
        ParrySuccess
    }

    [CreateAssetMenu(menuName = "Tutorial/Step", fileName = "New Tutorial Step")]
    public class TutorialStepDefinition : ScriptableObject
    {
        [Header("Display")]
        [TextArea(2, 5)]
        [SerializeField] private string promptText;
        [SerializeField] private float minimumDisplayDuration = 0f;

        [Header("Cutscene Behaviour For This Step")]
        [Tooltip("Stops player movement (physics) while this step is active.")]
        [SerializeField] private bool freezeMovement;
        [Tooltip("Disables player input handling while this step is active.")]
        [SerializeField] private bool disableInput;

        [Header("Completion Condition")]
        [SerializeField] private TutorialStepConditionType conditionType = TutorialStepConditionType.Prompt;

        [Header("Button Press Settings — used when Condition Type = Button Press")]
        [SerializeField] private InputActionReference buttonPressAction;

        [Header("Defeat Enemies Settings — used when Condition Type = Defeat Enemies")]
        [SerializeField] private int requiredKills = 1;
        [Tooltip("Leave empty to count any enemy death.")]
        [SerializeField] private string enemyTagFilter;

        [Header("Parry Success Settings — used when Condition Type = Parry Success")]
        [SerializeField] private int requiredParries = 1;

        public string PromptText => promptText;
        public float MinimumDisplayDuration => minimumDisplayDuration;
        public bool FreezeMovement => freezeMovement;
        public bool DisableInput => disableInput;
        public TutorialStepConditionType ConditionType => conditionType;

        public ITutorialStepEvaluator CreateEvaluator()
        {
            switch (conditionType)
            {
                case TutorialStepConditionType.TimedText:
                    return new TimedStepEvaluator();
                case TutorialStepConditionType.ButtonPress:
                    return new ButtonPressStepEvaluator(buttonPressAction);
                case TutorialStepConditionType.DefeatEnemies:
                    return new DefeatEnemiesStepEvaluator(requiredKills, enemyTagFilter);
                case TutorialStepConditionType.ParrySuccess:
                    return new ParrySuccessStepEvaluator(requiredParries);
                case TutorialStepConditionType.Prompt:
                default:
                    return new PromptStepEvaluator();
            }
        }
    }

    public interface ITutorialStepEvaluator
    {
        void Begin(Player player, Action onComplete);
        void End();
    }

    #endregion

    #region Timed Text — no condition, just shows for MinimumDisplayDuration then auto-advances

    public class TimedStepEvaluator : ITutorialStepEvaluator
    {
        public void Begin(Player player, Action onComplete) => onComplete?.Invoke();
        public void End() { }
    }

    #endregion

    #region Prompt — waits for the UI to call TutorialDirector.NotifyPlayerContinued()

    public class PromptStepEvaluator : ITutorialStepEvaluator
    {
        private Action onComplete;

        public void Begin(Player player, Action onComplete)
        {
            this.onComplete = onComplete;
            if (TutorialDirector.Instance != null)
                TutorialDirector.Instance.PlayerContinued += HandleContinued;
        }

        public void End()
        {
            if (TutorialDirector.Instance != null)
                TutorialDirector.Instance.PlayerContinued -= HandleContinued;
        }

        private void HandleContinued() => onComplete?.Invoke();
    }

    #endregion

    #region Button Press — "press X to continue"

    public class ButtonPressStepEvaluator : ITutorialStepEvaluator
    {
        private readonly InputActionReference actionRef;
        private Action onComplete;

        public ButtonPressStepEvaluator(InputActionReference actionRef) => this.actionRef = actionRef;

        public void Begin(Player player, Action onComplete)
        {
            this.onComplete = onComplete;
            if (actionRef == null || actionRef.action == null)
            {
                Debug.LogWarning("[Tutorial] ButtonPress step has no InputActionReference assigned.");
                return;
            }
            actionRef.action.performed += HandlePerformed;
        }

        public void End()
        {
            if (actionRef != null && actionRef.action != null)
                actionRef.action.performed -= HandlePerformed;
        }

        private void HandlePerformed(InputAction.CallbackContext ctx) => onComplete?.Invoke();
    }

    #endregion

    #region Defeat Enemies — subscribes to EnemyHealth.OnDeath on enemies present when the step starts

    public class DefeatEnemiesStepEvaluator : ITutorialStepEvaluator
    {
        private readonly int required;
        private readonly string tagFilter;
        private int killCount;
        private Action onComplete;
        private EnemyHealth[] trackedEnemies;

        public DefeatEnemiesStepEvaluator(int required, string tagFilter)
        {
            this.required = Mathf.Max(1, required);
            this.tagFilter = tagFilter;
        }

        public void Begin(Player player, Action onComplete)
        {
            this.onComplete = onComplete;
            killCount = 0;

            var all = UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
            trackedEnemies = string.IsNullOrEmpty(tagFilter)
                ? all
                : Array.FindAll(all, e => e.CompareTag(tagFilter));

            foreach (var enemy in trackedEnemies)
                enemy.OnDeath += HandleEnemyDeath;
        }

        public void End()
        {
            if (trackedEnemies == null) return;
            foreach (var enemy in trackedEnemies)
                if (enemy != null) enemy.OnDeath -= HandleEnemyDeath;
        }

        private void HandleEnemyDeath()
        {
            killCount++;
            if (killCount >= required) onComplete?.Invoke();
        }
    }

    #endregion

    #region Parry Success — requires PlayerCombatController.OnParrySuccess (see integration notes)

    public class ParrySuccessStepEvaluator : ITutorialStepEvaluator
    {
        private readonly int required;
        private int count;
        private Action onComplete;
        private PlayerCombatController combat;

        public ParrySuccessStepEvaluator(int required) => this.required = Mathf.Max(1, required);

        public void Begin(Player player, Action onComplete)
        {
            this.onComplete = onComplete;
            count = 0;
            combat = player != null ? player.CombatController : null;
            if (combat != null) combat.OnParrySuccess += HandleParrySuccess;
            else Debug.LogWarning("[Tutorial] ParrySuccess step could not find a PlayerCombatController.");
        }

        public void End()
        {
            if (combat != null) combat.OnParrySuccess -= HandleParrySuccess;
        }

        private void HandleParrySuccess()
        {
            count++;
            if (count >= required) onComplete?.Invoke();
        }
    }

    #endregion
}