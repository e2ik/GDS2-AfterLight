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
        [SerializeField] private TutorialTriggerUIMode uiMode = TutorialTriggerUIMode.TutorialUI;

        [SerializeField] private string triggerID;

        [SerializeField] private TutorialSequenceDefinition sequence;

        [Tooltip("If > 0, aborts the active sequence once the player moves this far from the volume. 0 = disabled.")]
        [SerializeField] private float maxDistanceFromPlayer = 0f;

        [TextArea(2, 4)]
        [SerializeField] private string speechBubbleText;
        [SerializeField] private float speechBubbleDuration = 3f;

        private bool sequenceActiveFromThisVolume;

        private string ResolvedGateID
        {
            get
            {
                if (uiMode == TutorialTriggerUIMode.SpeechBubble)
                    return string.IsNullOrEmpty(triggerID) ? null : triggerID;

                return sequence != null ? sequence.SequenceID : null;
            }
        }

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
            if (uiMode != TutorialTriggerUIMode.SpeechBubble || string.IsNullOrEmpty(triggerID)) return;

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

            string gateID = ResolvedGateID;
            if (!string.IsNullOrEmpty(gateID) && TutorialDirector.Instance != null && TutorialDirector.Instance.IsSequenceCompleted(gateID))
                return;

            if (uiMode == TutorialTriggerUIMode.TutorialUI || uiMode == TutorialTriggerUIMode.Both)
            {
                if (TutorialDirector.Instance != null && sequence != null)
                {
                    bool started = TutorialDirector.Instance.BeginSequence(sequence);
                    if (started) sequenceActiveFromThisVolume = true;
                }
            }

            if (uiMode == TutorialTriggerUIMode.SpeechBubble || uiMode == TutorialTriggerUIMode.Both)
            {
                if (TutorialSpeechBubblePool.Instance != null && !string.IsNullOrEmpty(speechBubbleText))
                {
                    Transform followTarget = GameManager.Instance != null && GameManager.Instance.Player != null
                        ? GameManager.Instance.Player.transform
                        : other.transform;

                    TutorialSpeechBubblePool.Instance.Show(speechBubbleText, followTarget, speechBubbleDuration);

                    if (uiMode == TutorialTriggerUIMode.SpeechBubble && !string.IsNullOrEmpty(gateID))
                        TutorialDirector.Instance?.MarkCompleted(gateID);
                }
            }
        }
    }
}