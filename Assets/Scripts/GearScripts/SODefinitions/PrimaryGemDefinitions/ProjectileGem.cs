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
        Vector2 velocity = Vector2.right * flySpeedMultiplier;
        GameObject projectile = Instantiate(projectilePrefab,context.OriginPoint,Quaternion.identity);
        PlayerProjectileBase projectileScript = projectile.GetComponent<PlayerSkillProjectile>();
        projectileScript.Launch(context.OriginPoint,velocity,damage,context);
    }
}
