using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Enemies;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "PiercingGem", menuName = "Primary Gems/PiercingGem")]
public class PiercingGem : PrimaryGemBehaviourDefinition
{
    [SerializeField]
    private float hitBoxWidth;
    [SerializeField]
    private float travelSpeed;
    [SerializeField] private float chargeRangeBonus = 2f;

    [SerializeField]
    private GameObject testVisPrefab;

    private Vector2 direction;
    
    public override void Execute(AttackContext context, float baseDamage, float chargeAmount = 0f)
    {
        Debug.Log("Pierce To Win");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.Log("player not found in skill execution");
            return;
        }
        PlayerCombatController pCombat = player.GetComponent<PlayerCombatController>();
        Vector2 center = player.transform.position;
        direction = player.GetComponent<Player>()?.Controller?.FacingDirection == 1 ? Vector2.right : Vector2.left;
        context.Runner.StartCoroutine(AttackRoutine(context, pCombat, baseDamage, chargeAmount));
    }

    private IEnumerator AttackRoutine(AttackContext context, PlayerCombatController playerCombat, float baseDamage, float chargeAmount)
    {
        float distanceTravelled = 0f;
        var enemiesHit = new HashSet<Collider2D>();
        var testVis = Instantiate(testVisPrefab,context.OriginPoint, Quaternion.identity);
        
        float skillDamage = baseDamage * SkillDamageModifier;
        float skillRange = SkillRange + chargeRangeBonus * chargeAmount;
        while (distanceTravelled < skillRange)
        {
            float step = travelSpeed * Time.deltaTime;
            distanceTravelled += step;

            Vector2 hitPosition = context.OriginPoint + direction * distanceTravelled;
            Collider2D[] hitsAtPos = Physics2D.OverlapBoxAll(hitPosition, new Vector2(hitBoxWidth, hitBoxWidth), 0f, playerCombat.enemyLayer);
            if (hitsAtPos.Count() > 0)
            {
                foreach (var col in hitsAtPos)
                {
                    if(enemiesHit.Contains(col)) continue;
                    enemiesHit.Add(col);
                    if(!col.CompareTag("EnemyHurtBox"))continue;
                    if(col.transform.root.TryGetComponent(out EnemyHealth enemyHealth)){
                        enemyHealth.ApplyDamage((int)skillDamage);
                    }
                }
            }
            UpdateVisual(testVis,hitPosition);
            yield return null;
        }
        Destroy(testVis);
    }

    private void UpdateVisual(GameObject proj, Vector2 pos)
    {
        DrawDebugBox(pos,new Vector2(hitBoxWidth,hitBoxWidth),0.05f);
        if(proj == null) return;
        proj.transform.SetPositionAndRotation(pos, Quaternion.identity);
    }
    private void DrawDebugBox(Vector2 center, Vector2 size, float duration = 0f)
{
    Vector2 halfSize = size * 0.5f;

    Vector2 topLeft = center + new Vector2(-halfSize.x, halfSize.y);
    Vector2 topRight = center + new Vector2(halfSize.x, halfSize.y);
    Vector2 bottomLeft = center + new Vector2(-halfSize.x, -halfSize.y);
    Vector2 bottomRight = center + new Vector2(halfSize.x, -halfSize.y);

    Debug.DrawLine(topLeft, topRight, Color.red, duration);
    Debug.DrawLine(topRight, bottomRight, Color.red, duration);
    Debug.DrawLine(bottomRight, bottomLeft, Color.red, duration);
    Debug.DrawLine(bottomLeft, topLeft, Color.red, duration);
}
}
