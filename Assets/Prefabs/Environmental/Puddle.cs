using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Puddle : MonoBehaviour
{
    [SerializeField] private Collider2D puddleCollider;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private string particleKey = "WaterSplash";
    [SerializeField, Min(0f)] private float splashCooldown = 0.35f;
    [SerializeField] private float splashHeight = 0f;
    [SerializeField, Min(0f)] private float minMoveSpeed = 0.5f;
    [SerializeField] private FMODUnity.EventReference splashEvent;

    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
    private float nextSplashTime;

    private void Awake()
    {
        if (puddleCollider == null)
            puddleCollider = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        occupants.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsOnPlayerLayer(other)) return;

        bool firstIn = occupants.Count == 0;
        occupants.Add(other);

        if (firstIn) TrySplash(other, ignoreCooldown: true);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsOnPlayerLayer(other)) return;
        if (!occupants.Contains(other)) return;

        TrySplash(other, ignoreCooldown: false);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        occupants.Remove(other);
    }

    private void TrySplash(Collider2D other, bool ignoreCooldown)
    {
        if (!ignoreCooldown && Time.time < nextSplashTime) return;
        if (!IsMoving(other)) return;

        nextSplashTime = Time.time + splashCooldown;

        Vector2 position = GetSplashPosition(other);
        PSpawner.Spawn(particleKey, position);
        AudioManager.PlaySFX(splashEvent, position);
    }

    private bool IsMoving(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        return body != null && body.linearVelocity.sqrMagnitude >= minMoveSpeed * minMoveSpeed;
    }

    private Vector2 GetSplashPosition(Collider2D other)
    {
        Vector2 playerCenter = other.bounds.center;
        Vector2 contactPoint = puddleCollider != null
            ? puddleCollider.ClosestPoint(playerCenter)
            : playerCenter;

        return new Vector2(contactPoint.x, splashHeight);
    }

    private bool IsOnPlayerLayer(Collider2D other)
    {
        return ((1 << other.gameObject.layer) & playerLayer.value) != 0;
    }
}