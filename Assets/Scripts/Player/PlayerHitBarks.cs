using Tutorial;
using UnityEngine;

public class PlayerHitBarks : MonoBehaviour
{
    [SerializeField] private string[] lines = { "Ouch!", "Ow!", "Argh!" };
    [SerializeField, Range(0f, 1f)] private float chance = 0.5f;
    [SerializeField, Min(0f)] private float cooldown = 6f;
    [SerializeField, Min(0f)] private float duration = 1.5f;
    [SerializeField] private DialogueEffect effect = DialogueEffect.Default;
    [SerializeField] private bool skipDuringTutorials = true;
    [SerializeField] private Transform target;

    private float nextAllowedTime;
    private int lastLineIndex = -1;

    private void Awake()
    {
        if (target == null) target = transform;
    }

    private void OnEnable() => TutorialEvents.OnRaised += HandleEvent;
    private void OnDisable() => TutorialEvents.OnRaised -= HandleEvent;

    private void HandleEvent(string key)
    {
        if (key != TutorialEvents.PlayerHit) return;
        if (lines == null || lines.Length == 0) return;
        if (Time.time < nextAllowedTime) return;
        if (skipDuringTutorials && TutorialDirector.Instance != null && TutorialDirector.Instance.IsRunningSequence) return;
        if (TutorialSpeechBubblePool.Instance == null) return;
        if (Random.value >= chance) return;

        nextAllowedTime = Time.time + cooldown;
        TutorialSpeechBubblePool.Instance.Show(PickLine(), target, duration, effect);
    }

    private string PickLine()
    {
        if (lines.Length == 1) return lines[0];

        int index;
        if (lastLineIndex < 0 || lastLineIndex >= lines.Length)
        {
            index = Random.Range(0, lines.Length);
        }
        else
        {
            index = Random.Range(0, lines.Length - 1);
            if (index >= lastLineIndex) index++;
        }

        lastLineIndex = index;
        return lines[index];
    }
}