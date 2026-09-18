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

        [Header("Tutorial UI — sequence-driven, step-tracked, save-gated")]
        [SerializeField] private TutorialSequenceDefinition sequence;

        [Header("Speech Bubble — standalone flavor text, not save-gated")]
        [TextArea(2, 4)]
        [SerializeField] private string speechBubbleText;
        [SerializeField] private float speechBubbleDuration = 3f;

        [SerializeField] private bool disableAfterTriggering = true;

        private void Reset()
        {
            if (TryGetComponent(out Collider2D col)) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            bool triggeredSomething = false;

            if (uiMode == TutorialTriggerUIMode.TutorialUI || uiMode == TutorialTriggerUIMode.Both)
            {
                if (TutorialDirector.Instance != null && sequence != null)
                    triggeredSomething |= TutorialDirector.Instance.BeginSequence(sequence);
            }

            if (uiMode == TutorialTriggerUIMode.SpeechBubble || uiMode == TutorialTriggerUIMode.Both)
            {
                if (TutorialSpeechBubblePool.Instance != null && !string.IsNullOrEmpty(speechBubbleText))
                {
                    Transform followTarget = GameManager.Instance != null && GameManager.Instance.Player != null
                        ? GameManager.Instance.Player.transform
                        : other.transform;

                    TutorialSpeechBubblePool.Instance.Show(speechBubbleText, followTarget, speechBubbleDuration);
                    triggeredSomething = true;
                }
            }

            if (triggeredSomething && disableAfterTriggering) gameObject.SetActive(false);
        }
    }
}