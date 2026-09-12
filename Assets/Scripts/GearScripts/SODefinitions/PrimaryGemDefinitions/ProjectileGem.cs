using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileGem", menuName = "Primary Gems/ProjectileGem")]
public class ProjectileGem : PrimaryGemBehaviourDefinition
{

    [SerializeField] GameObject projectilePrefab;
    [SerializeField] int damage;
    [SerializeField] float lifeTime;
    [SerializeField] float flySpeedMultiplier;

    public override void Execute(AttackContext context, float baseDamage, float chargeAmount = 0)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.Log("player not found in skill execution");
            return;
        }

        int facing = player.GetComponent<Player>()?.Controller?.FacingDirection ?? 1;
        Vector2 velocity = new Vector2(facing, 0f) * flySpeedMultiplier;

        float skillDamage = baseDamage * SkillDamageModifier;

        GameObject projectile = Instantiate(
            projectilePrefab,
            context.OriginPoint,
            Quaternion.Euler(0, 0, facing == 1 ? 90 : -90) // or 270, depending on your sprite's default orientation
        );

        PlayerProjectileBase projectileScript = projectile.GetComponent<PlayerSkillProjectile>();
        projectileScript.Launch(context.OriginPoint, velocity, (int)skillDamage, context);
    }
}
