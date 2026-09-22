using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Switch : MonoBehaviour, IInteractable
{
    [SerializeField] private List<GameObject> targetObjects = new();
    [SerializeField] private bool startOn = false;
    [SerializeField] private string onPrompt = "Turn off";
    [SerializeField] private string offPrompt = "Turn on";

    [Header("Indicator")]
    [SerializeField] private SpriteRenderer indicatorRenderer;
    [SerializeField] private Color onColor = Color.green;
    [SerializeField] private Color offColor = Color.red;
    [SerializeField] private Color inBetweenColor = Color.yellow;
    [SerializeField, Min(0f)] private float inBetweenDuration = 0.2f;

    [Header("Switch Sprite")]
    [SerializeField] private SpriteRenderer switchRenderer;
    [SerializeField] private Sprite switchOnSprite;
    [SerializeField] private Sprite switchOffSprite;
    [SerializeField] private Sprite switchInBetweenSprite;

    private readonly List<IOnOff> targets = new();
    private Coroutine visualRoutine;

    public bool IsOn { get; private set; }
    public string InteractionPrompt => IsOn ? onPrompt : offPrompt;
    public bool CanInteract => true;
    public bool ShouldStopPlayerMovement => false;
    public IReadOnlyList<IOnOff> Targets => targets;

    private void Awake()
    {
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
        Apply(!IsOn);
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

        foreach (var target in targets)
        {
            target.SetOn(on);
        }

        if (visualRoutine != null) StopCoroutine(visualRoutine);
        visualRoutine = isActiveAndEnabled ? StartCoroutine(TransitionVisual(on)) : null;

        if (visualRoutine == null)
            SetVisual(on ? onColor : offColor, on ? switchOnSprite : switchOffSprite);
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