using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class WallHazard : MonoBehaviour, IOnOff
{
    [Header("Sprites")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite inBetweenSprite;
    [SerializeField] private Sprite onSprite;

    [Header("Timing")]
    [SerializeField] private Vector2 offHoldRange = new(0.3f, 1.5f);
    [SerializeField] private Vector2 onHoldRange = new(0.4f, 2f);
    [SerializeField] private Vector2 transitionHoldRange = new(0.03f, 0.1f);

    [Header("Stutter")]
    [SerializeField, Range(0f, 1f)] private float stutterChance = 0.4f;
    [SerializeField] private Vector2Int stutterCountRange = new(1, 4);
    [SerializeField] private Vector2 stutterHoldRange = new(0.02f, 0.08f);

    [Header("Particles")]
    [SerializeField] private string particleKey = "Spark";
    [SerializeField] private Transform particleSpawnPoint;
    [SerializeField] private Vector2 particleIntervalRange = new(0.5f, 3f);
    [SerializeField] private bool particlesOnlyWhenOn = true;
    [SerializeField] private bool stopParticlesWhenInactive = true;
    [SerializeField] private FMODUnity.EventReference sparkEvent;

    [Header("Light")]
    [SerializeField] private Light2D hazardLight;
    [SerializeField, Min(0f)] private float offIntensity = 0f;
    [SerializeField, Min(0f)] private float inBetweenIntensity = 0.4f;
    [SerializeField, Min(0f)] private float onIntensity = 1f;
    [SerializeField, Min(0f)] private float sparkFlashIntensity = 1.5f;
    [SerializeField, Min(0.01f)] private float sparkFlashDuration = 0.15f;

    [Header("Hazard Collider (optional)")]
    [SerializeField] private Collider2D hazardCollider;

    private Coroutine flickerRoutine;
    private Coroutine particleRoutine;
    private readonly List<ParticleSystem> activeParticles = new();
    private float baseIntensity;
    private float flashAmount;

    public bool IsOn => enabled;
    public bool IsActive { get; private set; }

    private void OnEnable()
    {
        if (hazardCollider != null) hazardCollider.enabled = true;
        flickerRoutine = StartCoroutine(FlickerRoutine());
        particleRoutine = StartCoroutine(ParticleRoutine());
    }

    private void OnDisable()
    {
        if (flickerRoutine != null) StopCoroutine(flickerRoutine);
        if (particleRoutine != null) StopCoroutine(particleRoutine);
        flickerRoutine = null;
        particleRoutine = null;

        IsActive = false;
        if (hazardCollider != null) hazardCollider.enabled = false;
        SetSprite(offSprite);
        flashAmount = 0f;
        ApplyLight();

        StopActiveParticles();
    }

    private void StopActiveParticles()
    {
        foreach (var ps in activeParticles)
        {
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            PSpawner.Stop(ps);
        }
        activeParticles.Clear();
    }

    public void SetOn(bool on)
    {
        enabled = on;
    }

    private IEnumerator FlickerRoutine()
    {
        IsActive = false;
        SetSprite(offSprite);

        while (true)
        {
            yield return Wait(offHoldRange);

            if (Random.value < stutterChance)
            {
                int stutters = Random.Range(stutterCountRange.x, stutterCountRange.y + 1);
                for (int i = 0; i < stutters; i++)
                {
                    SetSprite(inBetweenSprite);
                    yield return Wait(stutterHoldRange);
                    SetSprite(offSprite);
                    yield return Wait(stutterHoldRange);
                }
            }

            SetSprite(inBetweenSprite);
            yield return Wait(transitionHoldRange);

            SetSprite(onSprite);
            IsActive = true;
            yield return Wait(onHoldRange);

            SetSprite(inBetweenSprite);
            IsActive = false;
            if (stopParticlesWhenInactive) StopActiveParticles();
            yield return Wait(transitionHoldRange);

            SetSprite(offSprite);
        }
    }

    private IEnumerator ParticleRoutine()
    {
        while (true)
        {
            yield return Wait(particleIntervalRange);

            if (particlesOnlyWhenOn && !IsActive) continue;

            Vector3 position = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;
            ParticleSystem ps = PSpawner.Spawn(particleKey, position);
            AudioManager.PlaySFX(sparkEvent, position);
            flashAmount = sparkFlashIntensity;

            if (ps != null)
            {
                activeParticles.RemoveAll(p => p == null || !p.isPlaying);
                activeParticles.Add(ps);
            }
        }
    }

    private void Update()
    {
        if (flashAmount <= 0f) return;

        flashAmount = Mathf.MoveTowards(flashAmount, 0f, sparkFlashIntensity / sparkFlashDuration * Time.deltaTime);
        ApplyLight();
    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;

        if (sprite == onSprite) baseIntensity = onIntensity;
        else if (sprite == inBetweenSprite) baseIntensity = inBetweenIntensity;
        else baseIntensity = offIntensity;

        ApplyLight();
    }

    private void ApplyLight()
    {
        if (hazardLight == null) return;

        float intensity = baseIntensity + flashAmount;
        hazardLight.intensity = intensity;
        hazardLight.enabled = intensity > 0f;
    }

    private static WaitForSeconds Wait(Vector2 range)
    {
        return new WaitForSeconds(Random.Range(range.x, range.y));
    }
}