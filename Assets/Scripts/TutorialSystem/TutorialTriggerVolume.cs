using System.Collections;
using UnityEngine;

namespace Tutorial
{
    public enum TutorialTriggerUIMode
    {
        TutorialUI,
        SpeechBubble,
        Both
    }

    [RequireComponent(typeof(Collider2D))]
    public class TutorialTriggerVolume : MonoBehaviour
    {
        [System.Serializable]
        private struct SpeechLine
        {
            [TextArea(2, 4)]
            public string text;
            public DialogueEffect effect;
        }

        [SerializeField] private TutorialTriggerUIMode uiMode = TutorialTriggerUIMode.TutorialUI;

        [SerializeField] private string triggerID;

        [SerializeField] private TutorialSequenceDefinition sequence;

        [Tooltip("If > 0, aborts the active sequence once the player moves this far from the volume. 0 = disabled.")]
        [SerializeField] private float maxDistanceFromPlayer = 0f;

        [TextArea(2, 4)]
        [SerializeField] private string speechBubbleText;
        [SerializeField] private float speechBubbleDuration = 3f;
        [SerializeField] private DialogueEffect speechBubbleEffect = DialogueEffect.Default;

        [Tooltip("SpeechBubble mode only. If any lines are set here, they play one after another (each shown for Speech Bubble Duration, each with its own effect) instead of the single Speech Bubble Text above. Leave empty to just use the single line as before.")]
        [SerializeField] private SpeechLine[] speechBubbleLines;

        [SerializeField] private bool hideVisualsOnStart = true;

        private bool sequenceActiveFromThisVolume;

        private bool UsesSequence => uiMode == TutorialTriggerUIMode.TutorialUI || uiMode == TutorialTriggerUIMode.Both;
        private bool UsesBubble => uiMode == TutorialTriggerUIMode.SpeechBubble || uiMode == TutorialTriggerUIMode.Both;

        private bool IsCompleted(string id) =>
            !string.IsNullOrEmpty(id) && TutorialDirector.Instance != null && TutorialDirector.Instance.IsSequenceCompleted(id);

        private bool SequenceDone => sequence == null || (!sequence.CanRepeat && IsCompleted(sequence.SequenceID));
        private bool BubbleDone => string.IsNullOrEmpty(triggerID) ? false : IsCompleted(triggerID);

        private void Reset()
        {
            if (TryGetComponent(out Collider2D col)) col.isTrigger = true;
            if (string.IsNullOrEmpty(triggerID)) triggerID = System.Guid.NewGuid().ToString("N");
        }

        [ContextMenu("Regenerate Trigger ID")]
        private void RegenerateTriggerID() => triggerID = System.Guid.NewGuid().ToString("N");

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!UsesBubble || string.IsNullOrEmpty(triggerID)) return;

            var all = FindObjectsByType<TutorialTriggerVolume>(FindObjectsSortMode.None);
            foreach (var other in all)
            {
                if (other != this && other.triggerID == triggerID)
                {
                    Debug.LogWarning(
                        $"[TutorialTriggerVolume] '{name}' shares triggerID '{triggerID}' with '{other.name}'.",
                        this);
                    break;
                }
            }
        }
#endif

        private void Awake()
        {
            if (!hideVisualsOnStart) return;

            foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color c = sr.color;
                c.a = 0f;
                sr.color = c;
            }
        }

        private void OnEnable()
        {
            if (TutorialDirector.Instance != null)
                TutorialDirector.Instance.OnSequenceCompleted += HandleSequenceCompleted;
        }

        private void OnDisable()
        {
            if (TutorialDirector.Instance != null)
            {
                TutorialDirector.Instance.OnSequenceCompleted -= HandleSequenceCompleted;

                if (sequenceActiveFromThisVolume)
                    TutorialDirector.Instance.AbortActiveSequence();
            }
            sequenceActiveFromThisVolume = false;
        }

        private void HandleSequenceCompleted(TutorialSequenceDefinition finishedSequence)
        {
            if (finishedSequence == sequence) sequenceActiveFromThisVolume = false;
        }

        private void Update()
        {
            if (!sequenceActiveFromThisVolume || maxDistanceFromPlayer <= 0f) return;

            Transform player = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.transform
                : null;
            if (player == null) return;

            float sqrDistance = (player.position - transform.position).sqrMagnitude;
            if (sqrDistance > maxDistanceFromPlayer * maxDistanceFromPlayer)
            {
                TutorialDirector.Instance?.AbortActiveSequence();
                sequenceActiveFromThisVolume = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (UsesSequence && !SequenceDone && TutorialDirector.Instance != null)
            {
                bool started = TutorialDirector.Instance.BeginSequence(sequence);
                if (started) sequenceActiveFromThisVolume = true;
            }

            if (UsesBubble && !BubbleDone)
                ShowSpeechBubble(other.transform);
        }

        private void ShowSpeechBubble(Transform fallbackTarget)
        {
            if (TutorialSpeechBubblePool.Instance == null) return;

            Transform followTarget = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.transform
                : fallbackTarget;

            bool useMultipleLines = speechBubbleLines != null && speechBubbleLines.Length > 0;
            bool shown = false;

            if (useMultipleLines)
            {
                StartCoroutine(PlaySpeechLinesRoutine(speechBubbleLines, followTarget));
                shown = true;
            }
            else if (!string.IsNullOrEmpty(speechBubbleText))
            {
                TutorialSpeechBubblePool.Instance.Show(speechBubbleText, followTarget, speechBubbleDuration, speechBubbleEffect);
                shown = true;
            }

            if (shown && !string.IsNullOrEmpty(triggerID))
                TutorialDirector.Instance?.MarkCompleted(triggerID);
        }

        private IEnumerator PlaySpeechLinesRoutine(SpeechLine[] lines, Transform target)
        {
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line.text)) continue;

                TutorialSpeechBubblePool.Instance.Show(line.text, target, speechBubbleDuration, line.effect);
                yield return new WaitForSeconds(speechBubbleDuration);
            }
        }
    }
}