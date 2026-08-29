using Enemies;
using UnityEngine;

[CreateAssetMenu(fileName = "Spin Attack Gem", menuName = "Primary Gems/Spin Attack Gem")]
public class SwordAOEGem : PrimaryGemBehaviourDefinition
{
    public override void Execute(AttackContext context, float baseDamage)
    {
        Debug.Log("Spin To Win");
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.Log("player not found in skill execution");
            return;
        }
        
        PlayerCombatController pCombat = player.GetComponent<PlayerCombatController>();
        Vector2 center = player.transform.position;
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(center, SkillRange, pCombat.enemyLayer);

        float skillDamage = baseDamage * SkillDamageModifier;
        
        foreach (var col in enemiesInRange)
        {
            if (!col.CompareTag("EnemyHurtBox"))
                continue;
            
            if(col.transform.root.TryGetComponent(out EnemyHealth enemyHealth)) 
                enemyHealth.ApplyDamage((int)skillDamage);
        }
    }
}
