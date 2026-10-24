using System.Collections.Generic;
using Enemies;
using UnityEngine;

public static class RunStats
{
    public static float TimePlayed { get; private set; }
    public static int EnemiesDefeated { get; private set; }
    public static int Deaths { get; private set; }
    public static int EpicsFound { get; private set; }
    public static int LegendariesFound { get; private set; }

    private static readonly HashSet<EnemyHealth> countedDead = new HashSet<EnemyHealth>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Reset();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        EnemyHealth.AnyEnemyDamaged -= HandleEnemyDamaged;
        PlayerStats.OnRespawnStarted -= HandleRespawnStarted;
        WorldItem.AnyItemCollected -= HandleItemCollected;
        EnemyHealth.AnyEnemyDamaged += HandleEnemyDamaged;
        PlayerStats.OnRespawnStarted += HandleRespawnStarted;
        WorldItem.AnyItemCollected += HandleItemCollected;

        GameObject runner = new GameObject("RunStatsTimer");
        runner.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<RunStatsTimer>();
    }

    public static void Reset()
    {
        TimePlayed = 0f;
        EnemiesDefeated = 0;
        Deaths = 0;
        EpicsFound = 0;
        LegendariesFound = 0;
        countedDead.Clear();
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        int hours = total / 3600;
        int minutes = (total / 60) % 60;
        int secs = total % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{secs:00}" : $"{minutes}:{secs:00}";
    }

    internal static void AddTime(float seconds) => TimePlayed += seconds;

    private static void HandleEnemyDamaged(EnemyHealth health, DamageInfo info)
    {
        if (health == null) return;

        if (health.CurrentHealth > 0)
        {
            countedDead.Remove(health);
            return;
        }

        if (countedDead.Add(health)) EnemiesDefeated++;
    }

    private static void HandleRespawnStarted() => Deaths++;

    private static void HandleItemCollected(WorldItem item, ERarity? rarity)
    {
        if (!rarity.HasValue) return;
        if (rarity.Value == ERarity.Epic) EpicsFound++;
        else if (rarity.Value == ERarity.Legendary) LegendariesFound++;
    }

    private class RunStatsTimer : MonoBehaviour
    {
        private GameManager.GameState lastState = GameManager.GameState.Title;

        private void Update()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null) return;

            GameManager.GameState state = manager.CurrentState;
            if (lastState == GameManager.GameState.Title && state == GameManager.GameState.Game) Reset();
            lastState = state;

            if (state == GameManager.GameState.Game && Time.timeScale > 0f) AddTime(Time.unscaledDeltaTime);
        }
    }
}
