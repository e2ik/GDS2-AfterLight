using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BlobShadow : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float originOffset = 0.05f;
    [SerializeField] private float groundOffset = 0.01f;

    [Header("Edge Fitting")]
    [SerializeField] private float surfaceTolerance = 1f;
    [SerializeField, Range(1, 8)] private int edgeSearchSteps = 5;

    [Header("Fade / Scale")]
    [SerializeField, Range(0f, 1f)] private float nearAlpha = 0.5f;
    [SerializeField, Range(0f, 1f)] private float farAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float farScaleMultiplier = 0.5f;

    private SpriteRenderer sr;
    private Rigidbody2D body;
    private Vector3 baseScale;
    private float baseHalfWidth;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        baseHalfWidth = sr.bounds.extents.x;

        if (bodyCollider == null)
            bodyCollider = GetComponentInParent<Collider2D>();

        if (bodyCollider != null)
            body = bodyCollider.attachedRigidbody;

        if (groundLayer.value == 0)
        {
            PlayerController controller = GetComponentInParent<PlayerController>();
            if (controller != null) groundLayer = controller.groundLayer;
        }
    }

    private void LateUpdate()
    {
        if (bodyCollider == null || !bodyCollider.enabled)
        {
            sr.enabled = false;
            return;
        }

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

        if (leftHit && rightHit && Mathf.Abs(left.point.y - right.point.y) > surfaceTolerance)
        {
            if (left.point.y > right.point.y) rightHit = false;
            else leftHit = false;
        }

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

        float refY = anchor.point.y;
        float minX = leftHit ? leftX : FindEdge(anchorX, leftX, originY, refY);
        float maxX = rightHit ? rightX : FindEdge(anchorX, rightX, originY, refY);

        float groundY = anchor.point.y;
        float distance = anchor.distance;
        if (leftHit && rightHit)
        {
            groundY = (left.point.y + right.point.y) * 0.5f;
            distance = (left.distance + right.distance) * 0.5f;
        }

        float t = Mathf.Clamp01(Mathf.Max(0f, distance - originOffset) / maxDistance);
        float distanceScale = Mathf.Lerp(1f, farScaleMultiplier, t);

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
        color.a = Mathf.Lerp(nearAlpha, farAlpha, t);
        sr.color = color;
    }

    private bool Probe(float x, float originY, out RaycastHit2D hit)
    {
        hit = Physics2D.Raycast(new Vector2(x, originY), Vector2.down, maxDistance + originOffset, groundLayer);
        return hit.collider != null;
    }

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