using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tutorial
{
    public class TutorialDirector : MonoBehaviour
    {
        public static TutorialDirector Instance { get; private set; }

        public event Action<TutorialSequenceDefinition> OnSequenceBegan;
        public event Action<TutorialSequenceDefinition> OnSequenceCompleted;
        public event Action<TutorialStepDefinition> OnStepBegan;
        public event Action<TutorialStepDefinition> OnStepEnded;
        public event Action OnCutsceneEntered;
        public event Action OnCutsceneExited;

        public event Action PlayerContinued;

        private readonly HashSet<string> completedSequenceIDs = new();

        private TutorialSequenceDefinition activeSequence;
        private TutorialStepDefinition activeStep;
        private ITutorialStepEvaluator activeEvaluator;
        private Coroutine sequenceRoutine;

        private bool cutsceneMovementLocked;
        private bool cutsceneInputLocked;

        public bool IsRunningSequence => activeSequence != null;
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
        }

        public bool IsSequenceCompleted(string sequenceID) => completedSequenceIDs.Contains(sequenceID);

        public void MarkCompleted(string id)
        {
            if (!string.IsNullOrEmpty(id)) completedSequenceIDs.Add(id);
        }

        public void LoadCompletedSequences(IEnumerable<string> ids)
        {
            completedSequenceIDs.Clear();
            if (ids == null) return;
            foreach (var id in ids) completedSequenceIDs.Add(id);
        }

        public List<string> GetCompletedSequencesForSave() => new(completedSequenceIDs);

        public void ClearCompletedSequences() => completedSequenceIDs.Clear();

        public bool BeginSequence(TutorialSequenceDefinition sequence, bool force = false)
        {
            if (sequence == null || sequence.Steps == null || sequence.Steps.Length == 0) return false;
            if (IsRunningSequence) return false;
            if (!force && !sequence.CanRepeat && IsSequenceCompleted(sequence.SequenceID)) return false;

            sequenceRoutine = StartCoroutine(RunSequence(sequence));
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
            FinishSequence(activeSequence, markCompleted: false);
        }

        private void EndActiveStepAbruptly()
        {
            activeEvaluator?.End();
            activeEvaluator = null;

            if (activeStep != null)
            {
                OnStepEnded?.Invoke(activeStep);
                activeStep = null;
            }
        }

        public void NotifyPlayerContinued() => PlayerContinued?.Invoke();

        private IEnumerator RunSequence(TutorialSequenceDefinition sequence)
        {
            activeSequence = sequence;
            OnSequenceBegan?.Invoke(sequence);

            Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;

            foreach (var step in sequence.Steps)
            {
                if (step == null) continue;
                yield return RunStep(step, player);
            }

            FinishSequence(sequence, markCompleted: true);
        }

        private IEnumerator RunStep(TutorialStepDefinition step, Player player)
        {
            activeStep = step;

            bool needsLock = step.FreezeMovement || step.DisableInput;
            if (needsLock) SetCutsceneState(step.FreezeMovement, step.DisableInput);

            OnStepBegan?.Invoke(step);

            bool complete = false;
            activeEvaluator = step.CreateEvaluator();
            activeEvaluator.Begin(player, () => complete = true);

            float elapsed = 0f;
            while (!complete)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed < step.MinimumDisplayDuration)
                yield return new WaitForSeconds(step.MinimumDisplayDuration - elapsed);

            activeEvaluator.End();
            activeEvaluator = null;

            if (needsLock) SetCutsceneState(false, false);

            OnStepEnded?.Invoke(step);
            activeStep = null;
        }

        private void FinishSequence(TutorialSequenceDefinition sequence, bool markCompleted)
        {
            if (markCompleted && sequence != null && !sequence.CanRepeat) completedSequenceIDs.Add(sequence.SequenceID);
            activeSequence = null;
            sequenceRoutine = null;
            OnSequenceCompleted?.Invoke(sequence);
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