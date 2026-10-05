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
        ParrySuccess,
        GameEvent,
        HealthNotFull,
        HealthFull,
        WallJumpClimb,
        SkillEnergyEnough
    }

    public enum TutorialStepRequirement
    {
        Always,
        HealthNotFull,
        HealthFull,
        SkillEnergyNotEnough,
        SkillEnergyEnough
    }

    [CreateAssetMenu(menuName = "Tutorial/Step", fileName = "New Tutorial Step")]
    public class TutorialStepDefinition : ScriptableObject
    {
        [Header("Display")]
        [TextArea(2, 5)]
        [SerializeField] private string promptText;
        [SerializeField] private float minimumDisplayDuration = 0f;
        [SerializeField, Min(0f)] private float startDelay = 0f;
        [SerializeField] private bool showSuccessOnComplete = false;

        [Header("Cutscene Behaviour For This Step")]
        [Tooltip("Stops player movement (physics) while this step is active.")]
        [SerializeField] private bool freezeMovement;
        [Tooltip("Disables player input handling while this step is active.")]
        [SerializeField] private bool disableInput;
        [SerializeField] private bool allowMenus;

        [Header("Run Requirement")]
        [SerializeField] private TutorialStepRequirement runOnlyIf = TutorialStepRequirement.Always;

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

        [Header("Game Event Settings — used when Condition Type = Game Event")]
        [SerializeField] private string gameEventKey = TutorialEvents.SwitchUsed;
        [SerializeField, Min(1)] private int requiredEventCount = 1;

        [Header("Wall Jump Climb Settings — used when Condition Type = Wall Jump Climb")]
        [SerializeField, Min(0.1f)] private float requiredHeightInPlayerHeights = 3f;

        public string PromptText => promptText;
        public float MinimumDisplayDuration => minimumDisplayDuration;
        public float StartDelay => startDelay;
        public bool ShowSuccessOnComplete => showSuccessOnComplete;
        public bool FreezeMovement => freezeMovement;
        public bool DisableInput => disableInput;
        public bool AllowMenus => allowMenus;
        public TutorialStepConditionType ConditionType => conditionType;
        public string GameEventKey => gameEventKey;

        public TutorialStepRequirement RunOnlyIf => runOnlyIf;

        public bool IsRequirementMet(Player player)
        {
            if (runOnlyIf == TutorialStepRequirement.Always) return true;

            if (runOnlyIf == TutorialStepRequirement.SkillEnergyNotEnough || runOnlyIf == TutorialStepRequirement.SkillEnergyEnough)
            {
                PlayerCombatController combat = player != null ? player.CombatController : null;
                if (combat == null) return true;
                bool enough = combat.HasEnoughSkillEnergy;
                return runOnlyIf == TutorialStepRequirement.SkillEnergyEnough ? enough : !enough;
            }

            PlayerStats stats = player != null ? player.Stats : null;
            if (stats == null) return true;

            bool isFull = stats.CurrentHealth >= stats.MaxHealth - 0.01f;

            switch (runOnlyIf)
            {
                case TutorialStepRequirement.HealthNotFull: return !isFull;
                case TutorialStepRequirement.HealthFull: return isFull;
                default: return true;
            }
        }

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
                case TutorialStepConditionType.GameEvent:
                    return new GameEventStepEvaluator(gameEventKey, requiredEventCount);
                case TutorialStepConditionType.HealthNotFull:
                    return new HealthStepEvaluator(waitForFull: false);
                case TutorialStepConditionType.HealthFull:
                    return new HealthStepEvaluator(waitForFull: true);
                case TutorialStepConditionType.WallJumpClimb:
                    return new WallJumpClimbStepEvaluator(requiredHeightInPlayerHeights);
                case TutorialStepConditionType.SkillEnergyEnough:
                    return new SkillEnergyStepEvaluator();
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

    #region Game Event — waits for TutorialEvents.Raise(key) from switches, chests, loot pickups etc.

    public class GameEventStepEvaluator : ITutorialStepEvaluator
    {
        private readonly string key;
        private readonly int required;
        private int count;
        private Action onComplete;

        public GameEventStepEvaluator(string key, int required)
        {
            this.key = key;
            this.required = Mathf.Max(1, required);
        }

        public void Begin(Player player, Action onComplete)
        {
            this.onComplete = onComplete;
            count = 0;

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[Tutorial] GameEvent step has no Game Event Key set.");
                return;
            }

            TutorialEvents.OnRaised += HandleRaised;
        }

        public void End()
        {
            TutorialEvents.OnRaised -= HandleRaised;
        }

        private void HandleRaised(string raisedKey)
        {
            if (raisedKey != key) return;

            count++;
            if (count >= required) onComplete?.Invoke();
        }
    }

    #endregion

    #region Health — completes when health is below max (HealthNotFull) or back at max (HealthFull)

    public class HealthStepEvaluator : ITutorialStepEvaluator
    {
        private readonly bool waitForFull;
        private Action onComplete;
        private PlayerStats stats;
        private bool done;

        public HealthStepEvaluator(bool waitForFull) => this.waitForFull = waitForFull;

        public void Begin(Player player, Action onComplete)
        {
            this.onComplete = onComplete;
            done = false;
            stats = player != null ? player.Stats : null;

            if (stats == null)
            {
                Debug.LogWarning("[Tutorial] Health step could not find PlayerStats.");
                return;
            }

            stats.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(stats.CurrentHealth, stats.MaxHealth);
        }

        public void End()
        {
            if (stats != null) stats.OnHealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (done) return;

            bool isFull = current >= max - 0.01f;
            if (isFull != waitForFull) return;

            done = true;
            onComplete?.Invoke();
        }
    }

    #endregion

    public class SkillEnergyStepEvaluator : ITutorialStepEvaluator
    {
        private Action onComplete;
        private PlayerCombatController combat;
        private bool done;

        public void Begin(Player player, Action onComplete)
        {
            this.onComplete = onComplete;
            done = false;
            combat = player != null ? player.CombatController : null;

            if (combat == null)
            {
                Debug.LogWarning("[Tutorial] Skill energy step could not find PlayerCombatController.");
                return;
            }

            combat.OnEnergyChanged += HandleEnergyChanged;
            HandleEnergyChanged(0f, 1f);
        }

        public void End()
        {
            if (combat != null) combat.OnEnergyChanged -= HandleEnergyChanged;
        }

        private void HandleEnergyChanged(float current, float max)
        {
            if (done || combat == null || !combat.HasEnoughSkillEnergy) return;

            done = true;
            onComplete?.Invoke();
        }
    }

    #region Wall Jump Climb — must wall jump, then land on ground at least N player-heights above the starting ground

    public class WallJumpClimbStepEvaluator : ITutorialStepEvaluator
    {
        private readonly float heightMultiplier;
        private Action onComplete;
        private Player player;
        private Coroutine watchRoutine;
        private float baselineY;
        private float requiredRise;
        private bool wallJumped;
        private bool done;

        public WallJumpClimbStepEvaluator(float heightMultiplier) => this.heightMultiplier = heightMultiplier;

        public void Begin(Player player, Action onComplete)
        {
            this.player = player;
            this.onComplete = onComplete;
            wallJumped = false;
            done = false;

            if (player == null || player.Controller == null || TutorialDirector.Instance == null)
            {
                Debug.LogWarning("[Tutorial] WallJumpClimb step could not find the player.");
                return;
            }

            baselineY = player.transform.position.y;
            requiredRise = GetPlayerHeight(player) * heightMultiplier;

            TutorialEvents.OnRaised += HandleRaised;
            watchRoutine = TutorialDirector.Instance.StartCoroutine(WatchRoutine());
        }

        public void End()
        {
            TutorialEvents.OnRaised -= HandleRaised;
            if (watchRoutine != null && TutorialDirector.Instance != null)
                TutorialDirector.Instance.StopCoroutine(watchRoutine);
            watchRoutine = null;
        }

        private void HandleRaised(string key)
        {
            if (key == TutorialEvents.PlayerWallJumped) wallJumped = true;
        }

        private System.Collections.IEnumerator WatchRoutine()
        {
            bool wasGrounded = player.Controller.IsGrounded;

            while (!done && player != null)
            {
                bool grounded = player.Controller.IsGrounded;

                if (grounded && !wasGrounded)
                {
                    float y = player.transform.position.y;

                    if (wallJumped && y >= baselineY + requiredRise)
                    {
                        done = true;
                        onComplete?.Invoke();
                        yield break;
                    }

                    if (y < baselineY) baselineY = y;
                    wallJumped = false;
                }

                wasGrounded = grounded;
                yield return null;
            }
        }

        private static float GetPlayerHeight(Player player)
        {
            float height = 0f;
            foreach (Collider2D col in player.GetComponentsInChildren<Collider2D>())
            {
                if (col.isTrigger) continue;
                height = Mathf.Max(height, col.bounds.size.y);
            }
            return height > 0f ? height : 1f;
        }
    }

    #endregion
}