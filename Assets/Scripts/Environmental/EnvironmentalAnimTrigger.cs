using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
public class EnvironmentalAnimTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool triggerOnce = true;

    [Header("Cooldown Settings")]
    [SerializeField] private bool useCooldown = false;
    [SerializeField, Min(0f)] private float cooldownDuration = 1f;

    [Header("Animator Settings")]
    [SerializeField] private string animatorTriggerName = "Activate";
    [SerializeField] private bool useBoolInstead = false;
    [SerializeField] private string animatorBoolName = "IsActive";

    private Animator animator;
    private bool hasTriggered;
    private float nextTriggerTime;

    private readonly HashSet<Collider2D> occupants = new();

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnDisable()
    {
        occupants.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsOnPlayerLayer(other)) return;

        PruneOccupants();

        bool alreadyOccupied = occupants.Count > 0;
        occupants.Add(other);
        if (alreadyOccupied) return;

        if (triggerOnce && hasTriggered) return;
        if (useCooldown && Time.time < nextTriggerTime) return;

        hasTriggered = true;
        nextTriggerTime = Time.time + cooldownDuration;

        if (useBoolInstead)
            animator.SetBool(animatorBoolName, true);
        else
            animator.SetTrigger(animatorTriggerName);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsOnPlayerLayer(other)) return;

        occupants.Remove(other);
        PruneOccupants();

        if (occupants.Count > 0) return;
        if (triggerOnce) return;

        if (useBoolInstead)
            animator.SetBool(animatorBoolName, false);
    }

    private void PruneOccupants()
    {
        occupants.RemoveWhere(c => c == null || !c.isActiveAndEnabled);
    }

    private bool IsOnPlayerLayer(Collider2D other)
    {
        return ((1 << other.gameObject.layer) & playerLayer) != 0;
    }
}