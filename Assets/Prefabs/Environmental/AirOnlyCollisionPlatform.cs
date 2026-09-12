using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AirOnlyCollisionPlatform : MonoBehaviour
{
    [SerializeField] private Vector2 solidLocalDirection = Vector2.up;
    [SerializeField] private float clearanceBuffer = 0.05f;

    private Collider2D platformCollider;
    private BoxCollider2D boxCollider;
    private Player cachedPlayer;
    private Collider2D[] playerColliders;
    private bool currentlyIgnoring;

    private void Awake()
    {
        platformCollider = GetComponent<Collider2D>();
        boxCollider = platformCollider as BoxCollider2D;
    }

    private void FixedUpdate()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindFirstObjectByType<Player>();
            if (cachedPlayer == null) return;
            playerColliders = cachedPlayer.GetComponentsInChildren<Collider2D>(true);
        }

        bool nearSolidFace = IsNearSolidFace();
        bool wantIgnore = cachedPlayer.Controller.IsGrounded || !nearSolidFace;

        bool shouldIgnore = wantIgnore || (currentlyIgnoring && IsWithinBuffer());

        if (shouldIgnore != currentlyIgnoring)
        {
            currentlyIgnoring = shouldIgnore;
            foreach (var col in playerColliders)
            {
                if (col != null) Physics2D.IgnoreCollision(col, platformCollider, shouldIgnore);
            }
        }
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

    private bool IsWithinBuffer()
    {
        foreach (var col in playerColliders)
        {
            if (col == null) continue;
            ColliderDistance2D dist = col.Distance(platformCollider);
            if (dist.distance < clearanceBuffer) return true;
        }
        return false;
    }

    public bool AllowsWallSlideFrom(Vector2 hitNormal)
    {
        Vector2 worldSolidDir = transform.TransformDirection(solidLocalDirection.normalized);
        return Vector2.Dot(hitNormal.normalized, worldSolidDir) > 0.7f;
    }
}