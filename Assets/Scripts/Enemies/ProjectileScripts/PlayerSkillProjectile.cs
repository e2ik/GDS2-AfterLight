using System;
using System.Collections.Generic;
using System.Linq;
using Enemies;
using UnityEditor.Rendering.LookDev;
using UnityEngine;

public class PlayerSkillProjectile : PlayerProjectileBase
{
    [SerializeField] private float gravityScale = 0f;
    [SerializeField] private float explosionRadius = 1.5f;
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private LayerMask collideWithMask;
    private bool hasTriggered = false;
    
    protected override void OnLaunch(Vector2 initialVelocity)
    {
        hasTriggered = false;
        Rb.gravityScale = gravityScale;
        Rb.linearVelocity = initialVelocity;
        
    }

    protected override bool OnHitTrigger(Collider2D other)
    {
        if(hasTriggered) return false;
        if(other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform))
        {
            return false;
        }
        
        if(((1 << other.gameObject.layer) & collideWithMask) != 0)
        {
            hasTriggered = true;
            Explode(other,explosionRadius);
            return true;
        }
        return false;
    }

    private void Explode(Collider2D other, float radius)
    {
        Vector2 originPoint = other.transform.position;
        Collider2D[] hitsAtExplosionPoint = Physics2D.OverlapCircleAll(originPoint, radius,enemyMask);
        var enemiesHit = new HashSet<Collider2D>();
        if(hitsAtExplosionPoint.Count() > 0)
        {
            foreach(var col in hitsAtExplosionPoint)
            {
                if(enemiesHit.Contains(col)) continue;
                if(!col.CompareTag("EnemyHurtBox")) continue;

                if(col.transform.root.TryGetComponent(out EnemyHealth enemyHealth))
                {
                    enemyHealth.ApplyHit(GetAdjustedDamage(originPoint,col.transform.position,(float)Damage),Context);
                }
            }
        }
    }

    private int GetAdjustedDamage(Vector2 explosionOrigin,Vector2 targetPosition, float baseDamage)
    {
        float distance = Vector2.Distance(explosionOrigin,targetPosition);
        float inverseDistance = Mathf.Clamp01(1 - distance / explosionRadius);
        float adjustedDamage = baseDamage * inverseDistance;
        return (int)adjustedDamage;
    }
}
