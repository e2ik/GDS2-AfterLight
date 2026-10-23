using Enemies;
using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private DamageNumber damageNumberPrefab;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float randomSpawnRangeX = 0.5f;

    [Header("Resisted")]
    [SerializeField] private bool showResisted = true;
    [SerializeField] private Vector3 resistedOffset = new Vector3(0f, 0.45f, 0f);

    [Header("Combo")]
    [SerializeField] private bool showCombo = true;
    [SerializeField, Min(1)] private int minComboToShow = 2;
    [SerializeField] private Vector3 comboOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Blocked")]
    [SerializeField] private bool showBlocked = true;
    [SerializeField] private Vector3 blockedOffset = new Vector3(0f, 0.45f, 0f);

    [Header("Dodge")]
    [SerializeField] private bool showDodges = true;
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.25f;

    [Header("Player Popups")]
    [SerializeField] private Vector3 playerSpawnOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private bool showRollTiers = true;
    [SerializeField, Min(0f)] private float rollTierCooldown = 0.15f;

    private ERollTier lastRollTier = ERollTier.None;
    private float lastDodgeTime = float.NegativeInfinity;
    private bool lastDodgeWasPerfect;
    private float lastRollTierTime = float.NegativeInfinity;

    private void OnEnable()
    {
        EnemyHealth.AnyEnemyDamaged += Spawn;
        PlayerHurtBox.AnyAttackDodged += SpawnDodge;
    }

    private void OnDisable()
    {
        EnemyHealth.AnyEnemyDamaged -= Spawn;
        PlayerHurtBox.AnyAttackDodged -= SpawnDodge;
    }

    private Vector3 RandomOffset() => new Vector3(Random.Range(-randomSpawnRangeX, randomSpawnRangeX), 0f, 0f);

    private void Spawn(EnemyHealth enemy, DamageInfo info)
    {
        if (damageNumberPrefab == null) return;

        Vector3 position = info.Position + spawnOffset + RandomOffset();
        DamageNumber number = Instantiate(damageNumberPrefab, position, Quaternion.identity);
        number.Show(info);

        if (showCombo && info.ComboIndex >= minComboToShow && info.DamageType == EDamageType.Base
            && damageNumberPrefab.HasComboLabel(info.ComboIndex))
        {
            DamageNumber combo = Instantiate(damageNumberPrefab, position + comboOffset, Quaternion.identity);
            combo.ShowCombo(info);
        }

        if (showBlocked && info.Blocked > 0)
        {
            DamageNumber blocked = Instantiate(damageNumberPrefab, position + blockedOffset, Quaternion.identity);
            blocked.ShowBlocked(info);
        }

        if (showResisted && info.Resisted > 0)
        {
            DamageNumber resisted = Instantiate(damageNumberPrefab, position + resistedOffset, Quaternion.identity);
            resisted.ShowResisted(info);
        }

        if (showRollTiers && info.HasRoll && (info.DamageType == EDamageType.Base || info.DamageType == EDamageType.Skill))
            SpawnRollTier(info.RollTier);
    }

    private bool TryGetPlayerPosition(out Vector3 position)
    {
        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        position = player != null ? player.transform.position + playerSpawnOffset + RandomOffset() : Vector3.zero;
        return player != null;
    }

    private void SpawnRollTier(ERollTier tier)
    {
        if (!DamageNumber.HasRollText(tier)) return;
        if (tier == lastRollTier && Time.time - lastRollTierTime < rollTierCooldown) return;
        if (!TryGetPlayerPosition(out Vector3 position)) return;

        lastRollTier = tier;
        lastRollTierTime = Time.time;

        DamageNumber number = Instantiate(damageNumberPrefab, position, Quaternion.identity);
        number.ShowRollTier(tier);
    }

    private void SpawnDodge(HitBox hitbox, bool isPerfect)
    {
        if (!showDodges || damageNumberPrefab == null) return;

        bool onCooldown = Time.time - lastDodgeTime < dodgeCooldown;
        if (onCooldown && (lastDodgeWasPerfect || !isPerfect)) return;

        if (!TryGetPlayerPosition(out Vector3 position)) return;

        lastDodgeTime = Time.time;
        lastDodgeWasPerfect = isPerfect;

        DamageNumber number = Instantiate(damageNumberPrefab, position, Quaternion.identity);
        number.ShowDodge(isPerfect);
    }
}