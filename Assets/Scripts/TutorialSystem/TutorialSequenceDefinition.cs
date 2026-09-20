using UnityEngine;

namespace Tutorial
{
    [CreateAssetMenu(menuName = "Tutorial/Sequence", fileName = "New Tutorial Sequence")]
    public class TutorialSequenceDefinition : ScriptableObject
    {
        [Tooltip("Unique, stable ID used for save persistence.")]
        [SerializeField] private string sequenceID;

        [SerializeField] private TutorialStepDefinition[] steps;

        [Tooltip("If true, this sequence can run again after being completed (no save-gating).")]
        [SerializeField] private bool canRepeat = false;

        public string SequenceID => sequenceID;
        public TutorialStepDefinition[] Steps => steps;
        public bool CanRepeat => canRepeat;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(sequenceID))
                Debug.LogWarning($"{name}: TutorialSequenceDefinition has no sequenceID set — save persistence won't work.", this);
        }
    }
}