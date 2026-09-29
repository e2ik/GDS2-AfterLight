using Enemies;
using Enemies.ProjectileScripts;
using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private DamageNumber damageNumberPrefab;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float randomSpawnRangeX = 0.5f;

    [Header("Dodge")]
    [SerializeField] private bool showDodges = true;

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

        DamageNumber number = Instantiate(damageNumberPrefab, info.Position + spawnOffset + RandomOffset(), Quaternion.identity);
        number.Show(info);
    }

    private void SpawnDodge(HitBox hitbox, bool isPerfect)
    {
        if (!showDodges || damageNumberPrefab == null || hitbox == null) return;

        Projectile projectile = hitbox.GetComponentInParent<Projectile>();
        Enemy enemy = hitbox.GetComponentInParent<Enemy>();

        Transform source = projectile != null && projectile.Owner != null
            ? projectile.Owner
            : enemy != null ? enemy.transform : hitbox.transform;

        DamageNumber number = Instantiate(damageNumberPrefab, source.position + spawnOffset + RandomOffset(), Quaternion.identity);
        number.ShowDodge(isPerfect);
    }
}