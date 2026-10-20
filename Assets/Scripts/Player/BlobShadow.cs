using UnityEngine;

/// <summary>
/// Blob shadow for a 2D character. Raycasts down from the left and right sides of the shadow,
/// snaps to the ground, shrinks/fades with height, and fits itself to the ground's edges so it
/// never hangs in mid-air when the player is standing on a ledge.
/// Attach to a child SpriteRenderer (e.g. a circle sprite, pivot in the centre) of the player.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BlobShadow : MonoBehaviour
{
    [Header("Raycast")]
    [Tooltip("Leave empty to copy the PlayerController's ground layer.")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Collider used to find the feet. Auto-found on the parent if left empty.")]
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private float maxDistance = 5f;
    [Tooltip("Start the rays slightly above the feet so they still hit when standing on the ground.")]
    [SerializeField] private float originOffset = 0.05f;
    [Tooltip("Lift the shadow a hair off the ground to avoid clipping.")]
    [SerializeField] private float groundOffset = 0.01f;

    [Header("Edge Fitting")]
    [Tooltip("Ground hits further apart in height than this count as a different surface " +
             "(e.g. the floor far below a ledge). Raise it if steep slopes shrink the shadow.")]
    [SerializeField] private float surfaceTolerance = 1f;
    [Tooltip("Extra rays used to locate a ledge edge. More = more precise.")]
    [SerializeField, Range(1, 8)] private int edgeSearchSteps = 5;

    [Header("Fade / Scale")]
    [SerializeField, Range(0f, 1f)] private float nearAlpha = 0.5f;
    [SerializeField, Range(0f, 1f)] private float farAlpha = 0f;
    [Tooltip("Scale multiplier when the ground is at max distance (1 = no shrink).")]
    [SerializeField, Range(0f, 1f)] private float farScaleMultiplier = 0.5f;

    [Header("Climb Fade")]
    [SerializeField, Min(0f)] private float climbFadeOutTime = 0.1f;
    [SerializeField, Min(0f)] private float climbFadeInTime = 0.25f;

    private SpriteRenderer sr;
    private Rigidbody2D body;
    private PlayerController controller;
    private float climbVisibility = 1f;
    private Vector3 baseScale;
    private float baseHalfWidth;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        baseHalfWidth = sr.bounds.extents.x; // world-space half width at full size

        if (bodyCollider == null)
            bodyCollider = GetComponentInParent<Collider2D>();

        if (bodyCollider != null)
            body = bodyCollider.attachedRigidbody;

        controller = GetComponentInParent<PlayerController>();
        if (groundLayer.value == 0 && controller != null)
            groundLayer = controller.groundLayer;
    }

    // LateUpdate so the shadow follows the player's final, interpolated position.
    private void LateUpdate()
    {
        UpdateClimbVisibility();

        if (bodyCollider == null || !bodyCollider.enabled)
        {
            sr.enabled = false;
            return;
        }

        // With Rigidbody interpolation the player's transform (what you see) runs slightly ahead of
        // its physics position (what collider.bounds reports). Correct for that, otherwise the
        // shadow jitters back and forth relative to the sprite while moving.
        Vector2 visualOffset = body != null
            ? (Vector2)body.transform.position - body.position
            : Vector2.zero;

        Bounds bounds = bodyCollider.bounds;
        float centerX = bounds.center.x + visualOffset.x;
        float originY = bounds.min.y + visualOffset.y + originOffset;
        float leftX = centerX - baseHalfWidth;
        float rightX = centerX + baseHalfWidth;

        bool leftHit = Probe(leftX, originY, out RaycastHit2D left);
        bool rightHit = Probe(rightX, originY, out RaycastHit2D right);

        // Rays landed at very different heights (ledge with a floor far below):
        // stick to the higher surface and treat the other side as "no ground".
        if (leftHit && rightHit && Mathf.Abs(left.point.y - right.point.y) > surfaceTolerance)
        {
            if (left.point.y > right.point.y) rightHit = false;
            else leftHit = false;
        }

        // Pick a point known to be on the ground to search outwards from.
        RaycastHit2D anchor;
        float anchorX;
        if (leftHit) { anchor = left; anchorX = leftX; }
        else if (rightHit) { anchor = right; anchorX = rightX; }
        else if (Probe(centerX, originY, out anchor)) { anchorX = centerX; }
        else
        {
            sr.enabled = false;
            return;
        }

        // Find where the ground actually starts/ends across the shadow's full width.
        float refY = anchor.point.y;
        float minX = leftHit ? leftX : FindEdge(anchorX, leftX, originY, refY);
        float maxX = rightHit ? rightX : FindEdge(anchorX, rightX, originY, refY);

        // Ground height and distance for the fade.
        float groundY = anchor.point.y;
        float distance = anchor.distance;
        if (leftHit && rightHit)
        {
            groundY = (left.point.y + right.point.y) * 0.5f;
            distance = (left.distance + right.distance) * 0.5f;
        }

        // 0 = standing on the ground, 1 = at max distance.
        float t = Mathf.Clamp01(Mathf.Max(0f, distance - originOffset) / maxDistance);
        float distanceScale = Mathf.Lerp(1f, farScaleMultiplier, t);

        // Intersect the (height-scaled) shadow with the ground that actually exists.
        float halfWidth = baseHalfWidth * distanceScale;
        float clippedMin = Mathf.Max(minX, centerX - halfWidth);
        float clippedMax = Mathf.Min(maxX, centerX + halfWidth);
        float width = clippedMax - clippedMin;

        if (width <= 0.001f)
        {
            sr.enabled = false;
            return;
        }

        sr.enabled = true;

        transform.position = new Vector3((clippedMin + clippedMax) * 0.5f, groundY + groundOffset, transform.position.z);
        transform.localScale = new Vector3(
            baseScale.x * width / (2f * baseHalfWidth),
            baseScale.y * distanceScale,
            baseScale.z);

        Color color = sr.color;
        color.a = Mathf.Lerp(nearAlpha, farAlpha, t) * climbVisibility;
        sr.color = color;
    }

    private void UpdateClimbVisibility()
    {
        bool climbing = controller != null && controller.IsClimbing;
        float target = climbing ? 0f : 1f;
        float duration = climbing ? climbFadeOutTime : climbFadeInTime;

        climbVisibility = duration <= 0f
            ? target
            : Mathf.MoveTowards(climbVisibility, target, Time.deltaTime / duration);
    }

    private bool Probe(float x, float originY, out RaycastHit2D hit)
    {
        hit = Physics2D.Raycast(new Vector2(x, originY), Vector2.down, maxDistance + originOffset, groundLayer);
        return hit.collider != null;
    }

    // Binary search between a point on the ground and a point off it to find the edge.
    private float FindEdge(float hitX, float missX, float originY, float refY)
    {
        for (int i = 0; i < edgeSearchSteps; i++)
        {
            float midX = (hitX + missX) * 0.5f;

            if (Probe(midX, originY, out RaycastHit2D hit) && Mathf.Abs(hit.point.y - refY) <= surfaceTolerance)
                hitX = midX;
            else
                missX = midX;
        }

        return hitX;
    }
}