using UnityEngine;

namespace Tutorial
{
    [RequireComponent(typeof(Collider2D))]
    public class TutorialTriggerVolume : MonoBehaviour
    {
        [SerializeField] private TutorialSequenceDefinition sequence;
        [SerializeField] private bool disableAfterTriggering = true;

        private void Reset()
        {
            if (TryGetComponent(out Collider2D col)) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            if (TutorialDirector.Instance == null || sequence == null) return;

            bool started = TutorialDirector.Instance.BeginSequence(sequence);
            if (started && disableAfterTriggering) gameObject.SetActive(false);
        }
    }
}