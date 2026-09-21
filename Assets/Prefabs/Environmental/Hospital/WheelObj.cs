using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class WheelObj : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private LayerMask playerLayer;

    [Header("Nudge")]
    [SerializeField, Min(0.01f)] private float nudgeDistance = 1.5f;
    [SerializeField, Min(0.05f)] private float nudgeDuration = 0.6f;
    [SerializeField] private AnimationCurve nudgeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Ground")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField, Min(0f)] private float maxStepHeight = 0.15f;
    [SerializeField, Min(0f)] private float maxDropHeight = 0.15f;
    [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.6f;
    [SerializeField, Min(0f)] private float edgeInset = 0.1f;
    [SerializeField] private float groundOffset = 0f;
    [SerializeField, Min(0f)] private float heightSmoothing = 20f;
    [SerializeField, Min(0f)] private float initialSnapDistance = 5f;

    private const float GroundSampleSpacing = 0.1f;
    private const float WallGap = 0.02f;
    private const float SettleTolerance = 0.0005f;

    private Rigidbody2D rb;
    private BoxCollider2D box;
    private Vector2 halfSize;
    private Vector2 centerOffset;

    private readonly HashSet<Collider2D> occupants = new();

    private bool isNudging;
    private bool settled = true;
    private float nudgeStartX;
    private float nudgeDirection;
    private float nudgeSafeDistance;
    private float nudgeTime;
    private float nudgeElapsed;

    public bool IsNudging => isNudging;

    private float ProbeReach => Mathf.Max(0f, halfSize.x - edgeInset);

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();

        box.isTrigger = true;
        rb.bodyType = RigidbodyType2D.Kinematic;

        Vector2 scale = transform.lossyScale;
        halfSize = new Vector2(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y)) * 0.5f;
        Vector2 worldCenter = transform.TransformPoint(box.offset);
        centerOffset = worldCenter - (Vector2)transform.position;

        if (obstacleLayers.value == 0) obstacleLayers = groundLayer;

        if (groundLayer.value == 0)
            Debug.LogWarning($"{name}: Ground Layer isn't set on WheelBed, so it won't find any ground.", this);
    }

    private void Start()
    {
        Vector2 pos = rb.position;
        if (TryGetTargetY(pos.x, pos.y, out float y, initialSnapDistance))
            rb.position = new Vector2(pos.x, y);
    }

    private void OnDisable()
    {
        occupants.Clear();
        isNudging = false;
    }

    private void FixedUpdate()
    {
        if (!isNudging && settled) return;

        Vector2 pos = rb.position;
        float x = pos.x;

        if (isNudging)
        {
            nudgeElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(nudgeElapsed / nudgeTime);
            float progress = Mathf.Clamp01(nudgeCurve.Evaluate(t));

            x = nudgeStartX + nudgeDirection * nudgeSafeDistance * progress;
            if (t >= 1f) isNudging = false;
        }

        float y = pos.y;
        bool atTarget = true;

        if (TryGetTargetY(x, pos.y, out float targetY))
        {
            y = heightSmoothing > 0f
                ? Mathf.MoveTowards(pos.y, targetY, heightSmoothing * Time.fixedDeltaTime)
                : targetY;
            atTarget = Mathf.Abs(y - targetY) < SettleTolerance;
        }

        settled = !isNudging && atTarget;
        rb.MovePosition(new Vector2(x, y));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        PruneOccupants();

        bool firstIn = occupants.Count == 0;
        occupants.Add(other);
        if (!firstIn) return;

        float playerX = other.attachedRigidbody != null
            ? other.attachedRigidbody.position.x
            : other.transform.position.x;
        float dx = (rb.position.x + centerOffset.x) - playerX;

        float direction = Mathf.Abs(dx) < 0.01f
            ? (Random.value < 0.5f ? -1f : 1f)
            : Mathf.Sign(dx);

        Nudge(direction);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        occupants.Remove(other);
    }

    public void Nudge(float direction)
    {
        if (isNudging) return;

        float dir = direction >= 0f ? 1f : -1f;
        float safeDistance = FindSafeDistance(dir, nudgeDistance);
        if (safeDistance < 0.01f) return;

        nudgeDirection = dir;
        nudgeSafeDistance = safeDistance;
        nudgeStartX = rb.position.x;
        nudgeElapsed = 0f;

        nudgeTime = Mathf.Max(0.05f, nudgeDuration * (safeDistance / nudgeDistance));

        isNudging = true;
        settled = false;
    }

    private float FindSafeDistance(float dir, float wanted)
    {
        Vector2 pos = rb.position;
        float allowed = wanted;

        float slabBottom = BottomY(pos.y) + maxStepHeight + 0.02f;
        float slabTop = TopY(pos.y) - 0.02f;

        if (slabTop > slabBottom)
        {
            const float thickness = 0.05f;
            float leadingX = CenterX(pos.x) + dir * halfSize.x;

            Vector2 size = new(thickness, slabTop - slabBottom);
            Vector2 origin = new(leadingX - dir * thickness * 0.5f, (slabTop + slabBottom) * 0.5f);

            RaycastHit2D[] hits = Physics2D.BoxCastAll(
                origin, size, 0f, Vector2.right * dir, wanted + WallGap, obstacleLayers);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.attachedRigidbody == rb) continue;
                if (hit.normal.y >= minGroundNormalY) continue;

                allowed = Mathf.Min(allowed, hit.distance - WallGap);
            }
        }

        allowed = Mathf.Max(0f, allowed);

        float referenceBottom = BottomY(pos.y);
        float startX = CenterX(pos.x) + dir * ProbeReach;
        float safe = 0f;

        for (float d = GroundSampleSpacing; ; d += GroundSampleSpacing)
        {
            float dist = Mathf.Min(d, allowed);

            if (!TryGetGround(startX + dir * dist, referenceBottom, out float groundY)) break;

            referenceBottom = groundY;
            safe = dist;

            if (dist >= allowed) break;
        }

        return safe;
    }

    private bool TryGetTargetY(float posX, float currentY, out float targetY, float extraDrop = 0f)
    {
        float centerX = CenterX(posX);
        float bottom = BottomY(currentY);
        float reach = ProbeReach;

        bool found = false;
        float best = float.NegativeInfinity;

        if (TryGetGround(centerX - reach, bottom, out float left, extraDrop))
        {
            best = left;
            found = true;
        }

        if (TryGetGround(centerX + reach, bottom, out float right, extraDrop))
        {
            best = Mathf.Max(best, right);
            found = true;
        }

        targetY = best - (centerOffset.y - halfSize.y) + groundOffset;
        return found;
    }

    private bool TryGetGround(float x, float referenceBottom, out float groundY, float extraDrop = 0f)
    {
        Vector2 origin = new(x, referenceBottom + maxStepHeight);
        float length = maxStepHeight + maxDropHeight + extraDrop;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, length, groundLayer);
        if (hit.collider != null && hit.normal.y >= minGroundNormalY)
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        return false;
    }

    private float CenterX(float posX) => posX + centerOffset.x;
    private float BottomY(float posY) => posY + centerOffset.y - halfSize.y;
    private float TopY(float posY) => posY + centerOffset.y + halfSize.y;

    private bool IsPlayer(Collider2D other)
    {
        return ((1 << other.gameObject.layer) & playerLayer.value) != 0;
    }

    private void PruneOccupants()
    {
        occupants.RemoveWhere(c => c == null || !c.isActiveAndEnabled);
    }
}