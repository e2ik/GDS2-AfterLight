using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerEdgeDetection : MonoBehaviour
{
    [SerializeField] private bool enabled;
    
    [SerializeField] private float radius;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private PlayerController pController;
    private readonly HashSet<Collider2D> groundContacts = new();
    private bool isActive => groundContacts.Count == 0;

    private void Update()
    {
        if (!enabled || !isActive)
        {
            pController.onEdge = false;
            return;
        }
        Collider2D col = Physics2D.OverlapCircle(transform.position, radius, groundLayer);
        pController.onEdge = IsClimbableWall(col);
    }

    private bool IsClimbableWall(Collider2D col)
    {
        if (col == null) return false;
        if (col.TryGetComponent(out AirOnlyCollisionPlatform thing)) return false;
        if (col.TryGetComponent(out DropThroughPlatform thing2)) return false;
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsGroundLayer(other.gameObject.layer))
            return;
        groundContacts.Add(other);
        pController.onEdge = false;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsGroundLayer(other.gameObject.layer))
            groundContacts.Remove(other);
    }

    private void OnDisable()
    {
        groundContacts.Clear();
        if (pController != null)
            pController.onEdge = false;
    }

    private bool IsGroundLayer(int layer) => (groundLayer.value & (1 << layer)) != 0;

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
