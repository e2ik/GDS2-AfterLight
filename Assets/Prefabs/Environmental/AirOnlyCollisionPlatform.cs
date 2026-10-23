using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AirOnlyCollisionPlatform : MonoBehaviour
{
    [SerializeField] private Vector2 solidLocalDirection = Vector2.up;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField, Min(0f)] private float overlapTolerance = 0.02f;

    private Collider2D platformCollider;
    private BoxCollider2D boxCollider;
    private Player cachedPlayer;
    private Collider2D[] playerColliders;
    private bool ignoring;
    private float forceIgnoreUntil = float.NegativeInfinity;

    private void Awake()
    {
        platformCollider = GetComponent<Collider2D>();
        boxCollider = platformCollider as BoxCollider2D;
    }

    private void OnDisable()
    {
        if (ignoring) SetIgnore(playerColliders, false);
        ignoring = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
        {
            Physics2D.IgnoreCollision(collision.collider, platformCollider, true);
        }
    }

    private void FixedUpdate()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindFirstObjectByType<Player>();
            if (cachedPlayer == null) return;
            playerColliders = cachedPlayer.GetComponentsInChildren<Collider2D>(true);
            ignoring = false;
        }

        bool shouldIgnore = Time.time < forceIgnoreUntil
                            || cachedPlayer.Controller.IsGrounded
                            || cachedPlayer.Controller.IsDashing
                            || cachedPlayer.Controller.IsWallSlideBlocked
                            || !IsNearSolidFace()
                            || IsOverlapping(playerColliders);

        if (shouldIgnore == ignoring) return;

        SetIgnore(playerColliders, shouldIgnore);
        ignoring = shouldIgnore;
    }

    public void IgnoreCollisionFor(Collider2D[] colliders, float duration)
    {
        if (playerColliders == null) playerColliders = colliders;

        forceIgnoreUntil = Mathf.Max(forceIgnoreUntil, Time.time + duration);

        SetIgnore(playerColliders, true);
        ignoring = true;
    }

    private void SetIgnore(Collider2D[] colliders, bool ignore)
    {
        if (colliders == null) return;

        foreach (var col in colliders)
        {
            if (col != null) Physics2D.IgnoreCollision(col, platformCollider, ignore);
        }
    }

    private bool IsOverlapping(Collider2D[] colliders)
    {
        if (colliders == null || platformCollider == null || !platformCollider.enabled) return false;

        foreach (var col in colliders)
        {
            if (col == null || !col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy) continue;

            ColliderDistance2D distance = col.Distance(platformCollider);
            if (distance.isValid && distance.distance < -overlapTolerance) return true;
        }

        return false;
    }

    private bool IsNearSolidFace()
    {
        Vector2 localOffset = transform.InverseTransformPoint(cachedPlayer.transform.position);

        Vector2 halfExtents = boxCollider != null
            ? Vector2.Scale(boxCollider.size, transform.lossyScale) * 0.5f
            : Vector2.one * 0.5f;

        Vector2 scaledOffset = new Vector2(
            halfExtents.x > 0f ? localOffset.x / halfExtents.x : 0f,
            halfExtents.y > 0f ? localOffset.y / halfExtents.y : 0f
        );

        Vector2 dominantAxis = Mathf.Abs(scaledOffset.x) > Mathf.Abs(scaledOffset.y)
            ? new Vector2(Mathf.Sign(scaledOffset.x), 0f)
            : new Vector2(0f, Mathf.Sign(scaledOffset.y));

        return Vector2.Dot(dominantAxis, solidLocalDirection.normalized) > 0.5f;
    }

    public bool AllowsSolidContactFrom(Vector2 hitNormal)
    {
        Vector2 worldSolidDir = transform.TransformDirection(solidLocalDirection.normalized);
        return Vector2.Dot(hitNormal.normalized, worldSolidDir) > 0.7f;
    }
}