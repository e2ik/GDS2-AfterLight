using UnityEngine;

public class HazardObj : MonoBehaviour
{
    public enum DirectionMode { Default = 0, SurfaceNormal = 2, PositionOnBounds = 3 }

    [SerializeField] private Collider2D hazardCollider;
    [SerializeField] private bool damagesPlayer = true;
    [SerializeField] private bool resetsPlayer = false;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0f)] private float knockbackForce = 12f;
    [SerializeField, Min(0f)] private float staggerDuration = 0.3f;
    [SerializeField] private DirectionMode directionMode = DirectionMode.Default;
    [SerializeField, Range(0f, 1f)] private float centerZone = 0.3f;
    [SerializeField, Range(0f, 90f)] private float maxAngle = 60f;
    [SerializeField] private bool debugLogs = false;

    private Player cachedPlayer;
    private PlayerHurtBox cachedHurtBox;

    private void Awake()
    {
        if (hazardCollider == null)
            hazardCollider = GetComponent<Collider2D>();
    }

    private void OnCollisionEnter2D(Collision2D col) => HandleCollision(col, "collision enter");
    private void OnCollisionStay2D(Collision2D col) => HandleCollision(col, "collision stay");
    private void OnTriggerEnter2D(Collider2D other) => HandleTrigger(other, "trigger enter");
    private void OnTriggerStay2D(Collider2D other) => HandleTrigger(other, "trigger stay");

    private void HandleCollision(Collision2D col, string source)
    {
        Log($"{source} with {col.collider.name}");

        if (!TryGetPlayer(col.collider, out Player player)) return;

        Vector2 contactPoint = transform.position;
        Vector2 normal = Vector2.zero;

        if (col.contactCount > 0)
        {
            ContactPoint2D contact = col.GetContact(0);
            contactPoint = contact.point;
            normal = contact.normal;
        }

        Vector2? direction = ResolveDirection(col.otherCollider, contactPoint, normal, col.collider.bounds.center);
        Hit(player, contactPoint, direction);
    }

    private void HandleTrigger(Collider2D other, string source)
    {
        Log($"{source} with {other.name}");

        if (!TryGetPlayer(other, out Player player)) return;

        Vector2 playerCenter = other.bounds.center;
        Vector2 contactPoint = hazardCollider != null
            ? hazardCollider.ClosestPoint(playerCenter)
            : (Vector2)transform.position;

        Vector2 normal = playerCenter - contactPoint;
        Vector2? direction = ResolveDirection(hazardCollider, contactPoint, normal, playerCenter);
        Hit(player, contactPoint, direction);
    }

    private void Hit(Player player, Vector2 contactPoint, Vector2? direction)
    {
        PlayerHurtBox hurtBox = GetHurtBox(player);
        if (hurtBox == null)
        {
            Log($"{player.name} has no PlayerHurtBox in its children, ignoring");
            return;
        }

        bool hit = hurtBox.TakeHazardHit(damage, contactPoint, knockbackForce, staggerDuration, direction, damagesPlayer, resetsPlayer);
        if (hit)
            Log($"hit: damages {damagesPlayer} (damage {damage}), force {knockbackForce}, stagger {staggerDuration}, direction {direction}");
    }

    private PlayerHurtBox GetHurtBox(Player player)
    {
        if (cachedPlayer != player)
        {
            cachedPlayer = player;
            cachedHurtBox = player.GetComponentInChildren<PlayerHurtBox>(true);
        }

        return cachedHurtBox;
    }

    private Vector2? ResolveDirection(Collider2D hazard, Vector2 contactPoint, Vector2 normal, Vector2 playerCenter)
    {
        switch (directionMode)
        {
            case DirectionMode.SurfaceNormal:
                return SurfaceDirection(contactPoint, normal, playerCenter);

            case DirectionMode.PositionOnBounds:
                return BoundsDirection(hazard, playerCenter);

            default:
                return null;
        }
    }

    private Vector2? BoundsDirection(Collider2D hazard, Vector2 playerCenter)
    {
        if (hazard == null) return null;

        Bounds bounds = hazard.bounds;
        if (bounds.extents.x < 0.0001f) return null;

        float t = Mathf.Clamp((playerCenter.x - bounds.center.x) / bounds.extents.x, -1f, 1f);

        float magnitude = Mathf.Abs(t);
        float zone = Mathf.Clamp01(centerZone);
        float scaled = magnitude <= zone ? 0f : (magnitude - zone) / Mathf.Max(0.0001f, 1f - zone);

        float angle = Mathf.Sign(t) * scaled * maxAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
    }

    private Vector2? SurfaceDirection(Vector2 contactPoint, Vector2 normal, Vector2 playerCenter)
    {
        if (normal.sqrMagnitude < 0.0001f) return null;

        Vector2 towardPlayer = playerCenter - contactPoint;
        return Vector2.Dot(normal, towardPlayer) >= 0f ? normal : -normal;
    }

    private bool TryGetPlayer(Collider2D other, out Player player)
    {
        player = null;

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null)
        {
            Log($"{other.name} has no Rigidbody2D attached, ignoring");
            return false;
        }

        if (!body.TryGetComponent(out player))
        {
            Log($"{body.name} has a Rigidbody2D but no Player on it, ignoring");
            return false;
        }

        return true;
    }

    private void Log(string message)
    {
        if (debugLogs) Debug.Log($"[HazardKnockback] {name}: {message}", this);
    }
}