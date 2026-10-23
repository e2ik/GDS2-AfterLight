using System.Collections.Generic;
using Enemies;
using UnityEngine;

public class MusicIntensityDriver : MonoBehaviour
{
    [Header("Enemies")]
    [SerializeField, Range(0f, 1f)] private float onScreenIntensity = 0.3f;
    [SerializeField, Range(0f, 1f)] private float engagedIntensity = 0.7f;
    [SerializeField, Range(0f, 1f)] private float perExtraEnemyIntensity = 0.1f;
    [SerializeField, Min(0f)] private float deaggroFadeDuration = 4f;
    [SerializeField] private bool bossHealthRaisesIntensity = true;

    [Header("Player Health")]
    [SerializeField] private AnimationCurve healthIntensity = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Fast Travel")]
    [SerializeField, Min(0f)] private float fastTravelFadeDuration = 1.5f;

    [Header("Smoothing")]
    [SerializeField, Min(0f)] private float riseSpeed = 1.5f;
    [SerializeField, Min(0f)] private float fallSpeed = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugIntensity = false;

    private static readonly HashSet<object> FastTravelSources = new HashSet<object>();
    private static readonly Dictionary<object, Vector2> IntensityZones = new Dictionary<object, Vector2>();

    private float current;
    private float combatFade;
    private float combatPeak;
    private float fastTravelWeight = 1f;
    private float lastLogged = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        FastTravelSources.Clear();
        IntensityZones.Clear();
    }

    public static void SetNearFastTravel(object source, bool near)
    {
        if (source == null) return;
        if (near) FastTravelSources.Add(source);
        else FastTravelSources.Remove(source);
    }

    public static void SetIntensityZone(object source, float minimum, float maximum)
    {
        if (source == null) return;
        IntensityZones[source] = new Vector2(Mathf.Clamp01(minimum), Mathf.Clamp01(maximum));
    }

    public static void ClearIntensityZone(object source)
    {
        if (source == null) return;
        IntensityZones.Remove(source);
    }

    private void Update()
    {
        MusicManager music = MusicManager.Instance;
        if (music == null) return;

        float dt = Time.unscaledDeltaTime;
        float target = ComputeTarget(dt, out float bossValue);
        float speed = target > current ? riseSpeed : fallSpeed;
        current = Mathf.MoveTowards(current, target, speed * dt);

        float weightTarget = FastTravelSources.Count > 0 ? 0f : 1f;
        fastTravelWeight = fastTravelFadeDuration > 0f
            ? Mathf.MoveTowards(fastTravelWeight, weightTarget, dt / fastTravelFadeDuration)
            : weightTarget;

        float output = Mathf.Clamp01(current * fastTravelWeight);

        if (music.IsBossMusicActive) music.SetBossIntensity(1f - output);
        else music.SetTargetIntensity(output);

        if (debugIntensity && Mathf.Abs(output - lastLogged) >= 0.05f)
        {
            lastLogged = output;
            Debug.Log($"[MusicIntensity] {output:0.00} (target {target:0.00}, engaged {EnemyCombatTracker.TargetingCount}, combat fade {combatFade:0.00}, health {GetHealthIntensity():0.00}, boss {bossValue:0.00}, fast travel weight {fastTravelWeight:0.00})", this);
        }
    }

    private float ComputeTarget(float dt, out float bossValue)
    {
        int engaged = EnemyCombatTracker.TargetingCount;
        float enemy = 0f;

        if (engaged > 0)
        {
            enemy = engagedIntensity + perExtraEnemyIntensity * (engaged - 1);
            combatPeak = enemy;
            combatFade = enemy;
        }
        else if (combatFade > 0f)
        {
            combatFade = deaggroFadeDuration > 0f
                ? Mathf.MoveTowards(combatFade, 0f, combatPeak / deaggroFadeDuration * dt)
                : 0f;
        }

        enemy = Mathf.Max(enemy, combatFade);

        if (onScreenIntensity > 0f && EnemyCombatTracker.AnyUnengagedOnScreen(Camera.main))
            enemy = Mathf.Max(enemy, onScreenIntensity);

        bossValue = bossHealthRaisesIntensity ? GetBossIntensity() : 0f;
        enemy = Mathf.Max(enemy, bossValue);

        float value = Mathf.Max(enemy, GetHealthIntensity());

        if (IntensityZones.Count > 0)
        {
            float minimum = 0f;
            float maximum = 1f;

            foreach (Vector2 zone in IntensityZones.Values)
            {
                minimum = Mathf.Max(minimum, zone.x);
                maximum = Mathf.Min(maximum, zone.y);
            }

            value = Mathf.Min(Mathf.Max(value, minimum), maximum);
        }

        return Mathf.Clamp01(value);
    }

    private float GetBossIntensity()
    {
        float best = 0f;

        foreach (Enemy enemy in EnemyCombatTracker.TargetingEnemies)
        {
            if (enemy == null || !enemy.IsBoss || enemy.Context == null || enemy.Context.Health == null) continue;

            EnemyHealth health = enemy.Context.Health;
            if (health.MaxHealth <= 0) continue;

            best = Mathf.Max(best, 1f - Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth));
        }

        return best;
    }

    private float GetHealthIntensity()
    {
        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        PlayerStats stats = player != null ? player.Stats : null;
        if (stats == null || stats.IsDead || stats.MaxHealth <= 0f || healthIntensity == null) return 0f;

        float healthPercent = Mathf.Clamp01(stats.CurrentHealth / stats.MaxHealth);
        return Mathf.Clamp01(healthIntensity.Evaluate(healthPercent));
    }
}
