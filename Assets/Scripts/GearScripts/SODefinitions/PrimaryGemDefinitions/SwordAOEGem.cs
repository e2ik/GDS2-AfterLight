using Enemies;
using UnityEngine;

[CreateAssetMenu(fileName = "Spin Attack Gem", menuName = "Primary Gems/Spin Attack Gem")]
public class SwordAOEGem : PrimaryGemBehaviourDefinition
{
    [SerializeField, Range(0f, 1f)] private float energyGainMultiplier = 0.5f;
    [SerializeField, Min(0f)] private float minEnergyGain = 0.01f;

    public override void Execute(AttackContext context, float baseDamage, float chargeAmount = 0f)
    {
        Debug.Log("Spin To Win");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.Log("player not found in skill execution");
            return;
        }
        AudioManager.PlaySFX(skillEvent);
        PlayerCombatController pCombat = player.GetComponent<PlayerCombatController>();
        Vector2 center = player.transform.position;
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(center, SkillRange, pCombat.enemyLayer);

        float skillDamage = baseDamage * (SkillDamageModifier + context.SkillModifierBonus);

        bool hitEnemy = false;
        foreach (var col in enemiesInRange)
        {
            if (!col.CompareTag("EnemyHurtBox"))
                continue;

            if (!col.transform.root.TryGetComponent(out EnemyHealth enemyHealth)) continue;
            enemyHealth.ApplyHit((int)skillDamage, context);
            hitEnemy = true;
        }

        if (!hitEnemy) return;

        if (context.ChargeAmount > 0f)
            context.ChargeAmount = Mathf.Max(context.ChargeAmount * energyGainMultiplier, minEnergyGain);

        pCombat.CheckEnergyChargePassive(isAttack: false, context);
    }
}