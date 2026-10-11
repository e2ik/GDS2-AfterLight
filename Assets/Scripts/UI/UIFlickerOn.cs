using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class UIFlickerOn : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color offColor = new Color(0.25f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color onColor = new Color(0.6f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color pulseColor = new Color(0.8f, 1f, 1f, 1f);

    [Header("Flicker")]
    [SerializeField, Min(0f)] private float startDelay = 0.1f;
    [SerializeField, Min(0.01f)] private float flickerDuration = 0.9f;
    [SerializeField, Min(0.01f)] private float flickerMinInterval = 0.03f;
    [SerializeField, Min(0.01f)] private float flickerMaxInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float flickerFadeSpeed = 25f;
    [SerializeField, Range(0f, 1f)] private float flickerJitter = 0.08f;
    [SerializeField, Min(0.01f)] private float settleFadeSpeed = 8f;

    [Header("Pulse")]
    [SerializeField] private bool pulseAfterOn = true;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 1.5f;
    [SerializeField, Min(0f)] private float pulseHoldOnStart = 0.2f;

    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool playOnEnable = false;

    private Graphic graphic;
    private Coroutine routine;
    private bool pendingPlay;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
    }

    private void OnEnable()
    {
        if (graphic == null) graphic = GetComponent<Graphic>();
        graphic.color = offColor;

        if (playOnEnable || pendingPlay) StartRun();
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    public void Play()
    {
        pendingPlay = true;
        if (graphic == null) graphic = GetComponent<Graphic>();
        graphic.color = offColor;
        if (isActiveAndEnabled) StartRun();
    }

    public void SetOff()
    {
        pendingPlay = false;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (graphic == null) graphic = GetComponent<Graphic>();
        graphic.color = offColor;
    }

    private void StartRun()
    {
        pendingPlay = false;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run());
    }

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += DeltaTime;
            yield return null;
        }
    }

    private IEnumerator Run()
    {
        yield return Wait(startDelay);

        float intensity = 0f;
        float target = 0f;
        float elapsed = 0f;
        float nextChange = 0f;

        while (elapsed < flickerDuration)
        {
            float dt = DeltaTime;
            elapsed += dt;
            nextChange -= dt;

            float progress = Mathf.Clamp01(elapsed / flickerDuration);

            if (nextChange <= 0f)
            {
                float low = Mathf.Lerp(0f, 0.6f, progress * progress);
                float high = Mathf.Lerp(0.5f, 1f, progress);
                target = Random.value < Mathf.Lerp(0.5f, 0.15f, progress) ? Random.Range(0f, low + 0.1f) : Random.Range(low, high);
                nextChange = Random.Range(flickerMinInterval, flickerMaxInterval);
            }

            intensity = Mathf.Lerp(intensity, target, 1f - Mathf.Exp(-flickerFadeSpeed * dt));
            float shown = Mathf.Clamp01(intensity + Random.Range(-flickerJitter, flickerJitter) * intensity);
            graphic.color = Color.Lerp(offColor, onColor, shown);

            yield return null;
        }

        while (intensity < 0.995f)
        {
            float dt = DeltaTime;
            intensity = Mathf.Lerp(intensity, 1f, 1f - Mathf.Exp(-settleFadeSpeed * dt));
            graphic.color = Color.Lerp(offColor, onColor, intensity);
            yield return null;
        }

        graphic.color = onColor;

        if (!pulseAfterOn)
        {
            routine = null;
            yield break;
        }

        yield return Wait(pulseHoldOnStart);

        float time = 0f;
        while (true)
        {
            time += DeltaTime;
            float t = 0.5f - 0.5f * Mathf.Cos(time * pulseSpeed * Mathf.PI * 2f);
            graphic.color = Color.Lerp(onColor, pulseColor, t);
            yield return null;
        }
    }
}