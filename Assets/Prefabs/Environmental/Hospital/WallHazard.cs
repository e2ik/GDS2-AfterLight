using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Hazard Collider (optional)")]
    [SerializeField] private Collider2D hazardCollider;

    private Coroutine flickerRoutine;
    private Coroutine particleRoutine;
    private readonly List<ParticleSystem> activeParticles = new();

    public bool IsOn { get; private set; }

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

        IsOn = false;
        if (hazardCollider != null) hazardCollider.enabled = false;
        SetSprite(offSprite);

        foreach (var ps in activeParticles)
        {
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
        IsOn = false;
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
            IsOn = true;
            yield return Wait(onHoldRange);

            SetSprite(inBetweenSprite);
            IsOn = false;
            yield return Wait(transitionHoldRange);

            SetSprite(offSprite);
        }
    }

    private IEnumerator ParticleRoutine()
    {
        while (true)
        {
            yield return Wait(particleIntervalRange);

            if (particlesOnlyWhenOn && !IsOn) continue;

            Vector3 position = particleSpawnPoint != null ? particleSpawnPoint.position : transform.position;
            ParticleSystem ps = PSpawner.Spawn(particleKey, position);

            if (ps != null)
            {
                activeParticles.RemoveAll(p => p == null || !p.isPlaying);
                activeParticles.Add(ps);
            }
        }
    }

    private void SetSprite(Sprite sprite)
    {
        if (spriteRenderer != null && sprite != null)
            spriteRenderer.sprite = sprite;
    }

    private static WaitForSeconds Wait(Vector2 range)
    {
        return new WaitForSeconds(Random.Range(range.x, range.y));
    }
}