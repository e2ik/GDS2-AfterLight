using System;
using Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using Random = UnityEngine.Random;

public class PlayerBarks : MonoBehaviour
{
    [Serializable]
    public class BarkLine
    {
        public string text;
        public DialogueEffect effect = DialogueEffect.Default;

        public BarkLine() { }

        public BarkLine(string text, DialogueEffect effect = DialogueEffect.Default)
        {
            this.text = text;
            this.effect = effect;
        }
    }

    [SerializeField] private Transform target;
    [SerializeField, Min(0f)] private float duration = 2f;
    [SerializeField] private bool skipDuringTutorials = true;
    [SerializeField] private bool skipDuringDialogue = true;

    [Header("Loot")]
    [SerializeField, Range(0f, 1f)] private float epicChance = 0.75f;
    [SerializeField, Range(0f, 1f)] private float legendaryChance = 0.9f;
    [SerializeField, Min(0f)] private float lootCooldown = 4f;
    [SerializeField] private BarkLine[] epicLines =
    {
        new BarkLine("Ooo... <color=#FFD54A>shiny</color>."),
        new BarkLine("Ooh, sparkly..."),
        new BarkLine("Mine now."),
        new BarkLine("Yoink!"),
        new BarkLine("<i>Very</i> nice."),
        new BarkLine("Come to me..."),
        new BarkLine("Is it... glowing?"),
        new BarkLine("Now THAT'S loot!", DialogueEffect.Angry)
    };
    [SerializeField] private BarkLine[] legendaryLines =
    {
        new BarkLine("<b>LEGENDARY?!</b>", DialogueEffect.Angry),
        new BarkLine("Oh. Oh my.", DialogueEffect.Unstable),
        new BarkLine("Wait... is that...?", DialogueEffect.Unstable),
        new BarkLine("It's <color=#FFB347>beautiful</color>...", DialogueEffect.Unstable),
        new BarkLine("Okay. I'm keeping this.", DialogueEffect.Angry),
        new BarkLine("Hands are shaking.", DialogueEffect.Unstable),
        new BarkLine("<size=120%>JACKPOT!</size>", DialogueEffect.Angry)
    };

    [Header("Idle")]
    [SerializeField] private bool idleBarksEnabled = true;
    [SerializeField, Min(1f)] private float idleDelay = 20f;
    [SerializeField, Min(1f)] private float idleRepeatInterval = 30f;
    [SerializeField, Range(0f, 1f)] private float idleChance = 0.85f;
    [SerializeField, Min(0f)] private float idleMoveThreshold = 0.1f;
    [SerializeField] private BarkLine[] idleLines =
    {
        new BarkLine("Player? You there?"),
        new BarkLine("Hello? Anyone?"),
        new BarkLine("I'll just... wait here."),
        new BarkLine("Did you forget me?"),
        new BarkLine("Snack break?"),
        new BarkLine("<i>*taps foot*</i>"),
        new BarkLine("Controller died?"),
        new BarkLine("Blink twice if you're okay."),
        new BarkLine("I can hear you breathing.", DialogueEffect.Unstable),
        new BarkLine("HELLO?!", DialogueEffect.Angry),
        new BarkLine("The tower won't climb itself."),
        new BarkLine("Still here. Still waiting.")
    };

    private float nextLootTime;
    private float lastActivityTime;
    private float nextIdleTime;
    private IDisposable anyButtonSubscription;
    private Rigidbody2D body;
    private BarkLine lastLine;

    private void Awake()
    {
        if (target == null) target = transform;
        body = GetComponentInParent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        WorldItem.AnyItemCollected += HandleItemCollected;
        anyButtonSubscription = InputSystem.onAnyButtonPress.Call(_ => MarkActive());
        MarkActive();
    }

    private void OnDisable()
    {
        WorldItem.AnyItemCollected -= HandleItemCollected;
        anyButtonSubscription?.Dispose();
        anyButtonSubscription = null;
    }

    private void MarkActive()
    {
        lastActivityTime = Time.unscaledTime;
        nextIdleTime = lastActivityTime + idleDelay;
    }

    private void Update()
    {
        if (body != null && body.linearVelocity.sqrMagnitude > idleMoveThreshold * idleMoveThreshold)
            MarkActive();

        if (!idleBarksEnabled || Time.unscaledTime < nextIdleTime) return;

        nextIdleTime = Time.unscaledTime + idleRepeatInterval;
        if (!CanBark() || Random.value >= idleChance) return;

        Say(PickLine(idleLines));
    }

    private void HandleItemCollected(WorldItem item, ERarity? rarity)
    {
        if (!rarity.HasValue || rarity.Value < ERarity.Epic) return;
        if (Time.time < nextLootTime || !CanBark()) return;

        bool legendary = rarity.Value == ERarity.Legendary;
        if (Random.value >= (legendary ? legendaryChance : epicChance)) return;

        BarkLine line = PickLine(legendary && legendaryLines != null && legendaryLines.Length > 0 ? legendaryLines : epicLines);
        if (line == null) return;

        nextLootTime = Time.time + lootCooldown;
        MarkActive();
        Say(line);
    }

    private bool CanBark()
    {
        if (Time.timeScale <= 0f || TutorialSpeechBubblePool.Instance == null) return false;
        if (skipDuringTutorials && TutorialDirector.Instance != null && TutorialDirector.Instance.IsRunningSequence) return false;
        if (skipDuringDialogue && DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) return false;
        return true;
    }

    private void Say(BarkLine line)
    {
        if (line == null || string.IsNullOrEmpty(line.text)) return;
        TutorialSpeechBubblePool.Instance.Show(line.text, target, duration, line.effect);
    }

    private BarkLine PickLine(BarkLine[] lines)
    {
        if (lines == null || lines.Length == 0) return null;
        if (lines.Length == 1) return lastLine = lines[0];

        BarkLine picked;
        int guard = 0;
        do
        {
            picked = lines[Random.Range(0, lines.Length)];
        } while (picked == lastLine && ++guard < 8);

        lastLine = picked;
        return picked;
    }
}