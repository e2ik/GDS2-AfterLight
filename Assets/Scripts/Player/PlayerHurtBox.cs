using System;
using Enemies;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerHurtBox : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private PlayerCombatController combatController;
    private PlayerController playerController;
    private Collider2D col;
    public bool Invulnerable;

    private void Awake()
    {
        if (stats == null)
            stats = GetComponentInParent<PlayerStats>();

        if (combatController == null)
            combatController = GetComponentInParent<PlayerCombatController>();

        if (playerController == null)
            playerController = GetComponentInParent<PlayerController>();

        if (col == null)
            col = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Invulnerable) return;
        
        //Debug.Log($"Player Hurt Box triggered by: {other.name}");

        var hitbox = other.GetComponent<HitBox>();
        if (hitbox == null || !hitbox.IsActive) return;

        bool parryWindowOpen = hitbox.SourceEvents != null && hitbox.SourceEvents.ParryWindowOpen;

        if (parryWindowOpen && combatController != null && combatController.CheckParry(hitbox.ParryDirection))
            return;
        
        stats.TakeDamage(hitbox.Damage);

        //Vector2 contactPoint = col.ClosestPoint(other.transform.position);
        //Vector2 direction = contactPoint - (Vector2)other.transform.root.transform.position;
        Vector2 sourcePosition = other.transform.root.transform.position;
        playerController.ApplyKnockback(sourcePosition, hitbox.AttackForce);
    }
}
