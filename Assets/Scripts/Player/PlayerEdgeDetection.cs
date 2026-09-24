using System;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerEdgeDetection : MonoBehaviour
{
    [SerializeField] private float radius;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private PlayerController pController;
    private int groundContacts;
    private bool isActive => groundContacts == 0;

    private void Update()
    {
        Debug.Log(isActive);
        pController.onEdge = isActive ? Physics2D.OverlapCircle(transform.position, radius, groundLayer) : false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsGroundLayer(other.gameObject.layer))
            groundContacts++;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsGroundLayer(other.gameObject.layer))
            groundContacts = Mathf.Max(0, groundContacts - 1);
    }

    private bool IsGroundLayer(int layer) => (groundLayer.value & (1 << layer)) != 0;

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
