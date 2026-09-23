using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Switch : MonoBehaviour, IInteractable
{
    public enum SwitchMode
    {
        Normal,
        RequireKey,
        Broken
    }

    [SerializeField] private List<GameObject> targetObjects = new();
    [SerializeField] private bool startOn = false;
    [SerializeField] private string onPrompt = "Turn off";
    [SerializeField] private string offPrompt = "Turn on";
    [SerializeField] private SpriteOutlineToggle outlineToggle;

    [Header("Access")]
    [SerializeField] private SwitchMode switchMode = SwitchMode.Normal;
    [Tooltip("Only used when Switch Mode is Require Key. The player must have a matching key in their inventory to interact.")]
    [SerializeField] private KeyDefinition requiredKey;
    [Tooltip("{0} is replaced with the required key's name, colored to match its inventory color, e.g. \"I need the [{0}]...\" -> \"I need the [Hospital Key]...\"")]
    [SerializeField] private string missingKeyMessage = "I need the [{0}]...";
    [SerializeField, Min(0f)] private float missingKeyMessageDuration = 2f;
    [SerializeField] private DialogueEffect missingKeyMessageEffect = DialogueEffect.Default;

    [Tooltip("Only used when Switch Mode is Broken. Interacting shows this message and never toggles anything.")]
    [SerializeField] private string brokenMessage = "This switch doesn't seem to work...";
    [SerializeField, Min(0f)] private float brokenMessageDuration = 2f;
    [SerializeField] private DialogueEffect brokenMessageEffect = DialogueEffect.Default;

    [Header("Indicator")]
    [SerializeField] private SpriteRenderer indicatorRenderer;
    [SerializeField] private Color onColor = Color.green;
    [SerializeField] private Color offColor = Color.red;
    [SerializeField] private Color inBetweenColor = Color.yellow;
    [SerializeField, Min(0f)] private float inBetweenDuration = 0.2f;
    [Tooltip("If on, the indicator/sprite shows the On look for as long as any wired target reports IsMoving (an IMovementIndicator, e.g. PositionSwitch), and the Off look otherwise — instead of the usual on/off + in-between transition.")]
    [SerializeField] private bool useMovementIndicator = false;

    [Header("Switch Sprite")]
    [SerializeField] private SpriteRenderer switchRenderer;
    [SerializeField] private Sprite switchOnSprite;
    [SerializeField] private Sprite switchOffSprite;
    [SerializeField] private Sprite switchInBetweenSprite;

    [Header("Audio")]
    [Tooltip("In Normal indicator mode, plays the moment the switch is pressed on/off. In Use Movement Indicator mode, plays when movement actually starts/stops instead.")]
    [SerializeField] private FMODUnity.EventReference onClickEvent;
    [SerializeField] private FMODUnity.EventReference offClickEvent;

    private readonly List<IOnOff> targets = new();
    private Coroutine visualRoutine;
    private bool lastAnyMoving;
    private bool isInitiator;

    public bool IsOn { get; private set; }
    public string InteractionPrompt => IsOn ? onPrompt : offPrompt;
    public bool CanInteract => !IsAnyTargetMoving();
    public bool ShouldStopPlayerMovement => false;
    public IReadOnlyList<IOnOff> Targets => targets;
    public SpriteOutlineToggle OutlineToggle => outlineToggle;

    private void Awake()
    {
        if (outlineToggle == null) outlineToggle = GetComponent<SpriteOutlineToggle>();

        foreach (var obj in targetObjects)
        {
            if (obj == null)
            {
                Debug.LogWarning($"{name}: a Target Objects slot is empty (None)", this);
                continue;
            }

            IOnOff[] found = obj.GetComponents<IOnOff>();

            if (found.Length == 0)
            {
                Debug.LogWarning($"{name}: {obj.name} has no component implementing IOnOff, skipping.", this);
                continue;
            }

            foreach (var onOff in found)
            {
                targets.Add(onOff);
            }
        }

        IsOn = startOn;
        foreach (var target in targets)
        {
            target.SetOn(startOn);
        }
        SetVisual(startOn ? onColor : offColor, startOn ? switchOnSprite : switchOffSprite);
    }

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        if (switchMode == SwitchMode.Broken)
        {
            if (player != null)
                Tutorial.TutorialSpeechBubblePool.Instance?.Show(brokenMessage, player.transform, brokenMessageDuration, brokenMessageEffect);

            return;
        }

        if (switchMode == SwitchMode.RequireKey && !PlayerHasRequiredKey(player))
        {
            if (player != null)
            {
                string message = missingKeyMessage;

                if (requiredKey != null)
                {
                    string coloredName = GameManager.Instance != null
                        ? $"<color=#{ColorUtility.ToHtmlStringRGB(GameManager.Instance.KeyItemColor)}>{requiredKey.UIName}</color>"
                        : requiredKey.UIName;

                    message = string.Format(missingKeyMessage, coloredName);
                }

                Tutorial.TutorialSpeechBubblePool.Instance?.Show(message, player.transform, missingKeyMessageDuration, missingKeyMessageEffect);
            }

            return;
        }

        Apply(!IsOn);
    }

    private bool PlayerHasRequiredKey(Player player)
    {
        if (requiredKey == null)
        {
            Debug.LogWarning($"{name}: Switch Mode is Require Key but no Required Key is assigned — allowing interaction.", this);
            return true;
        }

        PlayerInventorySO inv = player != null && player.Inventory != null ? player.Inventory.currentInventory : null;
        if (inv == null) return false;

        return inv.KeyInstances != null && inv.KeyInstances.Exists(k => k.InstItemID == requiredKey.ItemID);
    }

    public void SetOn(bool on)
    {
        Apply(on);
    }

    public void AddTarget(IOnOff target)
    {
        if (target == null || targets.Contains(target)) return;

        targets.Add(target);
        target.SetOn(IsOn);
    }

    public void RemoveTarget(IOnOff target)
    {
        targets.Remove(target);
    }

    private void Apply(bool on)
    {
        IsOn = on;
        isInitiator = true;

        foreach (var target in targets)
        {
            target.SetOn(on);
        }

        if (useMovementIndicator) return;

        AudioManager.PlaySFX(on ? onClickEvent : offClickEvent, transform.position, this);

        if (visualRoutine != null) StopCoroutine(visualRoutine);
        visualRoutine = isActiveAndEnabled ? StartCoroutine(TransitionVisual(on)) : null;

        if (visualRoutine == null)
            SetVisual(on ? onColor : offColor, on ? switchOnSprite : switchOffSprite);
    }

    private bool IsAnyTargetMoving()
    {
        foreach (var target in targets)
        {
            if (target is IMovementIndicator movable && movable.IsMoving) return true;
        }

        return false;
    }

    private void Update()
    {
        SyncWithTargets();

        if (!useMovementIndicator) return;

        bool anyMoving = IsAnyTargetMoving();

        if (anyMoving != lastAnyMoving)
        {
            if (isInitiator)
                AudioManager.PlaySFX(anyMoving ? onClickEvent : offClickEvent, transform.position, this);

            lastAnyMoving = anyMoving;

            if (!anyMoving) isInitiator = false;
        }

        SetVisual(anyMoving ? onColor : offColor, anyMoving ? switchOnSprite : switchOffSprite);
    }

    private void SyncWithTargets()
    {
        if (targets.Count == 0) return;

        bool targetsOn = targets[0].IsOn;
        if (targetsOn == IsOn) return;

        IsOn = targetsOn;

        if (!useMovementIndicator)
            SetVisual(IsOn ? onColor : offColor, IsOn ? switchOnSprite : switchOffSprite);
    }

    private IEnumerator TransitionVisual(bool on)
    {
        SetVisual(inBetweenColor, switchInBetweenSprite);

        if (inBetweenDuration > 0f)
            yield return new WaitForSeconds(inBetweenDuration);

        SetVisual(on ? onColor : offColor, on ? switchOnSprite : switchOffSprite);
        visualRoutine = null;
    }

    private void SetVisual(Color color, Sprite sprite)
    {
        if (indicatorRenderer != null)
            indicatorRenderer.color = color;

        if (switchRenderer != null && sprite != null)
            switchRenderer.sprite = sprite;
    }
}