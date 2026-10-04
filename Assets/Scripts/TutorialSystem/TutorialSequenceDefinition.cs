using UnityEngine;

namespace Tutorial
{
    [CreateAssetMenu(menuName = "Tutorial/Sequence", fileName = "New Tutorial Sequence")]
    public class TutorialSequenceDefinition : ScriptableObject
    {
        [System.Serializable]
        public class SequenceReaction
        {
            public string eventKey = TutorialEvents.PlayerHit;
            [TextArea(2, 4)] public string speechText;
            public DialogueEffect effect = DialogueEffect.Default;
            [Min(0f)] public float duration = 2.5f;
            public bool onlyOnce = true;
        }

        [Tooltip("Unique, stable ID used for save persistence.")]
        [SerializeField] private string sequenceID;

        [SerializeField] private TutorialStepDefinition[] steps;

        [Tooltip("If true, this sequence can run again after being completed (no save-gating).")]
        [SerializeField] private bool canRepeat = false;

        [SerializeField] private bool uninterruptible = false;
        [SerializeField] private bool keepEnemiesAlive = false;
        [SerializeField] private bool lockMapCloseUntilCloseStep = false;
        [SerializeField] private bool allowMenus = false;

        [SerializeField] private SequenceReaction[] reactions;

        public string SequenceID => sequenceID;
        public TutorialStepDefinition[] Steps => steps;
        public bool CanRepeat => canRepeat;
        public SequenceReaction[] Reactions => reactions;
        public bool Uninterruptible => uninterruptible;
        public bool KeepEnemiesAlive => keepEnemiesAlive;
        public bool LockMapCloseUntilCloseStep => lockMapCloseUntilCloseStep;
        public bool AllowMenus => allowMenus;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(sequenceID))
                Debug.LogWarning($"{name}: TutorialSequenceDefinition has no sequenceID set — save persistence won't work.", this);
        }
    }
}