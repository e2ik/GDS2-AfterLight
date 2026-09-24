using System;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerEdgeDetection : MonoBehaviour
{
    [SerializeField] private float radius;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private PlayerController pController;
    private bool active;

    private void Update()
    {
        if(active)
            pController.onEdge = Physics2D.OverlapCircle(transform.position, radius, groundLayer);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            active = false;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
            active = true;
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
