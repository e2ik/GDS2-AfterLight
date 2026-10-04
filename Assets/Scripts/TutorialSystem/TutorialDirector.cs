using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tutorial
{
    public class TutorialDirector : MonoBehaviour
    {
        [System.Serializable]
        public class EventSequenceTrigger
        {
            public string eventKey = TutorialEvents.ItemPickedUp;
            public TutorialSequenceDefinition sequence;
            public TutorialSequenceDefinition[] requiredCompletedSequences;
            public bool waitIfBusy = true;
        }

        public static TutorialDirector Instance { get; private set; }

        [SerializeField] private EventSequenceTrigger[] eventTriggers;
        [SerializeField] private bool debugEventTriggers = false;
        private readonly List<TutorialSequenceDefinition> pendingSequences = new();

        public event Action<TutorialSequenceDefinition> OnSequenceBegan;
        public event Action<TutorialSequenceDefinition> OnSequenceCompleted;
        public event Action<TutorialStepDefinition> OnStepBegan;
        public event Action<TutorialStepDefinition> OnStepEnded;
        public event Action<TutorialStepDefinition> OnStepCompleted;
        public event Action OnCutsceneEntered;
        public event Action OnCutsceneExited;
        public event Action PlayerContinued;

        private readonly HashSet<string> completedSequenceIDs = new();
        private readonly HashSet<UIWindowAnimator> excludedWindows = new();
        private readonly Dictionary<string, int> resumeIndices = new();

        private TutorialSequenceDefinition activeSequence;
        private TutorialStepDefinition activeStep;
        private ITutorialStepEvaluator activeEvaluator;
        private Coroutine sequenceRoutine;
        private int activeStepIndex;

        private bool cutsceneMovementLocked;
        private bool cutsceneInputLocked;

        [SerializeField] private bool pauseTimeOnPromptSteps = true;
        private bool timePausedByStep;
        private int stepHoldCount;
        private readonly HashSet<TutorialSequenceDefinition.SequenceReaction> firedReactions = new();
        private bool listeningForReactions;

        public void AddStepHold() => stepHoldCount++;
        public void RemoveStepHold() => stepHoldCount = Mathf.Max(0, stepHoldCount - 1);
        private float pausedTimeScale = 1f;

        public bool IsRunningSequence => activeSequence != null;

        public bool EnemiesProtected =>
            activeSequence != null
            && activeSequence.KeepEnemiesAlive
            && (activeStep == null || activeStep.ConditionType != TutorialStepConditionType.DefeatEnemies);
        public TutorialStepDefinition ActiveStep => activeStep;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            UIWindowAnimator.OnAnyShown += HandleAnyWindowShown;
            TutorialEvents.OnRaised += HandleTriggerEvent;
        }

        private void HandleTriggerEvent(string key)
        {
            if (eventTriggers == null) return;

            foreach (EventSequenceTrigger trigger in eventTriggers)
            {
                if (trigger == null || trigger.sequence == null || trigger.eventKey != key) continue;

                if (!trigger.sequence.CanRepeat && IsSequenceCompleted(trigger.sequence.SequenceID))
                {
                    LogTrigger(trigger, "skipped, sequence already completed");
                    continue;
                }

                if (!RequirementsMet(trigger.requiredCompletedSequences, out string missing))
                {
                    LogTrigger(trigger, $"skipped, required sequence '{missing}' not completed yet");
                    continue;
                }

                if (activeSequence == trigger.sequence || pendingSequences.Contains(trigger.sequence))
                {
                    LogTrigger(trigger, "skipped, already running or queued");
                    continue;
                }

                if (IsRunningSequence)
                {
                    if (trigger.waitIfBusy) pendingSequences.Add(trigger.sequence);
                    LogTrigger(trigger, trigger.waitIfBusy ? "queued, another sequence is running" : "skipped, another sequence is running");
                    continue;
                }

                bool started = BeginSequence(trigger.sequence);
                LogTrigger(trigger, started ? "started" : "BeginSequence refused (no steps or already completed)");
            }
        }

        private bool RequirementsMet(TutorialSequenceDefinition[] required, out string missing)
        {
            missing = null;
            if (required == null) return true;

            foreach (TutorialSequenceDefinition req in required)
            {
                if (req != null && !IsSequenceCompleted(req.SequenceID))
                {
                    missing = req.name;
                    return false;
                }
            }
            return true;
        }

        private void LogTrigger(EventSequenceTrigger trigger, string message)
        {
            if (debugEventTriggers)
                Debug.Log($"[TutorialDirector] Trigger '{trigger.eventKey}' -> '{trigger.sequence.name}': {message}", this);
        }

        private void TryStartPendingSequence()
        {
            while (pendingSequences.Count > 0 && !IsRunningSequence)
            {
                TutorialSequenceDefinition next = pendingSequences[0];
                pendingSequences.RemoveAt(0);
                if (next == null) continue;
                if (!next.CanRepeat && IsSequenceCompleted(next.SequenceID)) continue;

                BeginSequence(next);
            }
        }

        private void OnDestroy()
        {
            ResumeTime();
            StopReactions();
            if (Instance == this)
            {
                UIWindowAnimator.OnAnyShown -= HandleAnyWindowShown;
                TutorialEvents.OnRaised -= HandleTriggerEvent;
            }
        }

        public void RegisterExcludedWindow(UIWindowAnimator window)
        {
            if (window != null) excludedWindows.Add(window);
        }

        private void HandleAnyWindowShown(UIWindowAnimator window)
        {
            if (excludedWindows.Contains(window)) return;

            RaiseWindowEvents(window);

            if (activeStep != null && activeStep.AllowMenus) return;
            if (IsRunningSequence) AbortActiveSequence();
        }

        private static void RaiseWindowEvents(UIWindowAnimator window)
        {
            if (window == null) return;

            TutorialEvents.Raise(TutorialEvents.WindowOpenedPrefix + window.gameObject.name);

            if (window.GetComponentInParent<InventoryDisplay>(true) != null || window.GetComponentInChildren<InventoryDisplay>(true) != null)
                TutorialEvents.Raise(TutorialEvents.InventoryOpened);
        }

        public bool IsSequenceCompleted(string sequenceID) => completedSequenceIDs.Contains(sequenceID);

        public void MarkCompleted(string id)
        {
            if (!string.IsNullOrEmpty(id)) completedSequenceIDs.Add(id);
        }

        public void LoadCompletedSequences(IEnumerable<string> ids)
        {
            completedSequenceIDs.Clear();
            resumeIndices.Clear();
            if (ids == null) return;
            foreach (var id in ids) completedSequenceIDs.Add(id);
        }

        public List<string> GetCompletedSequencesForSave() => new(completedSequenceIDs);

        public void ClearCompletedSequences()
        {
            pendingSequences.Clear();
            completedSequenceIDs.Clear();
            resumeIndices.Clear();
        }

        public bool BeginSequence(TutorialSequenceDefinition sequence, bool force = false)
        {
            if (sequence == null || sequence.Steps == null || sequence.Steps.Length == 0) return false;
            if (!force && !sequence.CanRepeat && IsSequenceCompleted(sequence.SequenceID)) return false;

            if (IsRunningSequence)
            {
                if (activeSequence == sequence) return false;

                if (activeSequence.Uninterruptible && !force)
                {
                    if (activeSequence.QueueBlockedSequences && !pendingSequences.Contains(sequence))
                        pendingSequences.Add(sequence);
                    return false;
                }

                AbortActiveSequence();
            }

            int startIndex = resumeIndices.TryGetValue(sequence.SequenceID, out int savedIndex) ? savedIndex : 0;
            sequenceRoutine = StartCoroutine(RunSequence(sequence, startIndex));
            return true;
        }

        public void SkipActiveSequence()
        {
            if (!IsRunningSequence) return;
            if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
            EndActiveStepAbruptly();
            SetCutsceneState(false, false);
            FinishSequence(activeSequence, markCompleted: true);
        }

        public void AbortActiveSequence()
        {
            if (!IsRunningSequence) return;
            if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
            EndActiveStepAbruptly();
            SetCutsceneState(false, false);

            if (activeSequence != null) resumeIndices[activeSequence.SequenceID] = activeStepIndex;

            FinishSequence(activeSequence, markCompleted: false);
        }

        private void EndActiveStepAbruptly()
        {
            activeEvaluator?.End();
            activeEvaluator = null;

            ResumeTime();

            if (activeStep != null)
            {
                OnStepEnded?.Invoke(activeStep);
                activeStep = null;
            }
        }

        public void NotifyPlayerContinued() => PlayerContinued?.Invoke();

        private IEnumerator RunSequence(TutorialSequenceDefinition sequence, int startIndex)
        {
            activeSequence = sequence;
            stepHoldCount = 0;
            StartReactions();
            OnSequenceBegan?.Invoke(sequence);

            Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;

            for (int i = startIndex; i < sequence.Steps.Length; i++)
            {
                var step = sequence.Steps[i];
                if (step == null) continue;

                while (stepHoldCount > 0) yield return null;

                if (!step.IsRequirementMet(player)) continue;

                activeStepIndex = i;
                yield return RunStep(step, player);
            }

            FinishSequence(sequence, markCompleted: true);
        }

        private IEnumerator RunStep(TutorialStepDefinition step, Player player)
        {
            activeStep = step;

            bool pauseTime = pauseTimeOnPromptSteps && step.ConditionType == TutorialStepConditionType.Prompt;
            bool disableInput = step.DisableInput || pauseTime;
            bool needsLock = step.FreezeMovement || disableInput;
            bool freezeMovement = step.FreezeMovement || pauseTime;
            if (needsLock) SetCutsceneState(freezeMovement, disableInput);
            if (pauseTime) PauseTime();

            OnStepBegan?.Invoke(step);

            float elapsed = 0f;
            if (step.StartDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(step.StartDelay);
                elapsed = step.StartDelay;
            }

            bool complete = false;
            activeEvaluator = step.CreateEvaluator();
            activeEvaluator.Begin(player, () => complete = true);

            while (!complete)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (elapsed < step.MinimumDisplayDuration)
                yield return new WaitForSecondsRealtime(step.MinimumDisplayDuration - elapsed);

            activeEvaluator.End();
            activeEvaluator = null;

            ResumeTime();
            if (needsLock) SetCutsceneState(false, false);

            OnStepCompleted?.Invoke(step);
            OnStepEnded?.Invoke(step);
            activeStep = null;
        }

        private void PauseTime()
        {
            if (timePausedByStep) return;
            pausedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            timePausedByStep = true;
        }

        private void ResumeTime()
        {
            if (!timePausedByStep) return;
            Time.timeScale = pausedTimeScale > 0f ? pausedTimeScale : 1f;
            timePausedByStep = false;
        }

        private void StartReactions()
        {
            firedReactions.Clear();
            if (listeningForReactions) return;
            TutorialEvents.OnRaised += HandleReactionEvent;
            listeningForReactions = true;
        }

        private void StopReactions()
        {
            firedReactions.Clear();
            if (!listeningForReactions) return;
            TutorialEvents.OnRaised -= HandleReactionEvent;
            listeningForReactions = false;
        }

        private void HandleReactionEvent(string key)
        {
            if (activeSequence == null || activeSequence.Reactions == null) return;

            foreach (var reaction in activeSequence.Reactions)
            {
                if (reaction == null || reaction.eventKey != key) continue;
                if (string.IsNullOrEmpty(reaction.speechText)) continue;
                if (reaction.onlyOnce && firedReactions.Contains(reaction)) continue;

                firedReactions.Add(reaction);

                Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                if (player != null)
                    TutorialSpeechBubblePool.Instance?.Show(reaction.speechText, player.transform, reaction.duration, reaction.effect);
            }
        }

        private void FinishSequence(TutorialSequenceDefinition sequence, bool markCompleted)
        {
            StopReactions();

            if (markCompleted)
            {
                if (sequence != null && !string.IsNullOrEmpty(sequence.SequenceID)) completedSequenceIDs.Add(sequence.SequenceID);
                if (sequence != null) resumeIndices.Remove(sequence.SequenceID);
            }
            activeSequence = null;
            sequenceRoutine = null;
            OnSequenceCompleted?.Invoke(sequence);

            if (markCompleted) TryStartPendingSequence();
        }

        public void SetCutsceneState(bool freezeMovement, bool disableInput)
        {
            PlayerController controller = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.Controller
                : null;
            if (controller == null) return;

            bool wasInCutscene = cutsceneMovementLocked || cutsceneInputLocked;

            if (freezeMovement && !cutsceneMovementLocked)
            {
                controller.FreezeMovement(true);
                cutsceneMovementLocked = true;
            }
            else if (!freezeMovement && cutsceneMovementLocked)
            {
                controller.FreezeMovement(false);
                cutsceneMovementLocked = false;
            }

            if (disableInput && !cutsceneInputLocked)
            {
                controller.InputEnabled = false;
                cutsceneInputLocked = true;
            }
            else if (!disableInput && cutsceneInputLocked)
            {
                controller.InputEnabled = true;
                cutsceneInputLocked = false;
            }

            bool nowInCutscene = cutsceneMovementLocked || cutsceneInputLocked;
            if (nowInCutscene && !wasInCutscene) OnCutsceneEntered?.Invoke();
            else if (!nowInCutscene && wasInCutscene) OnCutsceneExited?.Invoke();
        }
    }
}