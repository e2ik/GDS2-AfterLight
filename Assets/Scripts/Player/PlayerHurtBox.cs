using System;
using Enemies;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PlayerHurtBox : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private PlayerCombatController combatController;
    public bool Invulnerable;

    private void Awake()
    {
        if (stats == null)
            stats = GetComponentInParent<PlayerStats>();

        if (combatController == null)
            combatController = GetComponentInParent<PlayerCombatController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Invulnerable) return;

        var hitbox = other.GetComponent<HitBox>();
        if (hitbox == null || !hitbox.IsActive) return;

        bool parryWindowOpen = hitbox.SourceEvents != null && hitbox.SourceEvents.ParryWindowOpen;

        if (parryWindowOpen && combatController != null && combatController.CheckParry(hitbox.ParryDirection))
        {
            combatController.OnParrySuccess(hitbox.gameObject);
            return;
        } 
        
        stats.TakeDamage(hitbox.Damage);
    }
}
