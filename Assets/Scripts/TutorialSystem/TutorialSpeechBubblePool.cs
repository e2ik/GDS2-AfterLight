using System.Collections.Generic;
using UnityEngine;

namespace Tutorial
{
    // any object can call TutorialSpeechBubblePool.Instance?.Show("Watch out!", transform, duration: 2f); as an example
    // There are pools set up for each target Transform, so you can have multiple bubbles active at once
    public class TutorialSpeechBubblePool : MonoBehaviour
    {
        public static TutorialSpeechBubblePool Instance { get; private set; }

        [SerializeField] private TutorialSpeechBubbleUI bubblePrefab;
        [Tooltip("Where spawned bubbles are parented — usually your UI Canvas's RectTransform. Defaults to this object if left empty.")]
        [SerializeField] private RectTransform poolParent;
        [SerializeField] private int prewarmCount = 3;

        private readonly Stack<TutorialSpeechBubbleUI> inactivePool = new();
        private readonly Dictionary<Transform, TutorialSpeechBubbleUI> activeByTarget = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (bubblePrefab == null)
            {
                Debug.LogError("[TutorialSpeechBubblePool] No bubblePrefab assigned.", this);
                return;
            }

            for (int i = 0; i < prewarmCount; i++)
                inactivePool.Push(CreateBubble());
        }

        private TutorialSpeechBubbleUI CreateBubble()
        {
            Transform parent = poolParent != null ? poolParent : transform;
            TutorialSpeechBubbleUI bubble = Instantiate(bubblePrefab, parent);
            bubble.gameObject.SetActive(false);
            bubble.OnHidden += HandleBubbleHidden;
            return bubble;
        }

        public TutorialSpeechBubbleUI Show(string text, Transform target, float duration = 0f)
        {
            if (target == null || bubblePrefab == null) return null;

            if (activeByTarget.TryGetValue(target, out var existing))
            {
                existing.Show(text, target, duration);
                return existing;
            }

            TutorialSpeechBubbleUI bubble = inactivePool.Count > 0 ? inactivePool.Pop() : CreateBubble();
            bubble.gameObject.SetActive(true);
            activeByTarget[target] = bubble;
            bubble.Show(text, target, duration);
            return bubble;
        }

        public void Hide(Transform target)
        {
            if (target != null && activeByTarget.TryGetValue(target, out var bubble))
                bubble.Hide();
        }

        private void HandleBubbleHidden(TutorialSpeechBubbleUI bubble)
        {
            Transform keyToRemove = null;
            foreach (var kvp in activeByTarget)
            {
                if (kvp.Value == bubble)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }
            if (keyToRemove != null) activeByTarget.Remove(keyToRemove);

            bubble.gameObject.SetActive(false);
            inactivePool.Push(bubble);
        }
    }
}