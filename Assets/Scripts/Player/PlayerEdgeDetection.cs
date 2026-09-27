using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerEdgeDetection : MonoBehaviour
{
    [FormerlySerializedAs("enabled")]
    [SerializeField] private bool detectionEnabled = true;
    [SerializeField] private LayerMask groundLayer;

    [Header("Grab Zone (relative to the top of the player's body)")]
    [SerializeField] private float grabZoneBottom = -0.4f;
    [SerializeField] private float grabZoneTop = 0.3f;
    [SerializeField, Min(0.01f)] private float reach = 0.2f;

    [Header("Surface Checks")]
    [SerializeField, Range(0f, 1f)] private float ledgeTopNormalThreshold = 0.7f;
    [SerializeField, Range(0f, 1f)] private float wallNormalThreshold = 0.7f;
    [SerializeField, Range(0.5f, 1f)] private float clearanceScale = 0.9f;

    private const float Skin = 0.02f;
    private const float WallProbeDepth = 0.05f;

    private ContactFilter2D filter;
    private readonly List<RaycastHit2D> rayHits = new();
    private readonly List<Collider2D> overlapHits = new();

    private bool hasLastQuery;
    private Bounds lastBody;
    private int lastDir;
    private bool lastFound;
    private Vector2 lastCorner;
    private Vector2 lastStandCenter;
    private Vector2 lastStandSize;

    private void Awake() => BuildFilter();
    private void OnValidate() => BuildFilter();

    private void BuildFilter()
    {
        filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(groundLayer);
    }

    public bool TryFindLedge(Bounds body, int dir, out Vector2 corner)
    {
        corner = default;

        hasLastQuery = true;
        lastBody = body;
        lastDir = dir;
        lastFound = false;
        lastStandSize = Vector2.zero;

        if (!detectionEnabled || dir == 0) return false;

        float front = dir > 0 ? body.max.x : body.min.x;
        float zoneTopY = body.max.y + grabZoneTop;
        float zoneBottomY = body.max.y + grabZoneBottom;

        Vector2 topOrigin = new(front + dir * reach, zoneTopY);
        if (!CastClosest(topOrigin, Vector2.down, zoneTopY - zoneBottomY, out RaycastHit2D top)) return false;

        if (top.distance <= 0f || top.normal.y < ledgeTopNormalThreshold) return false;

        float ledgeY = top.point.y;

        Vector2 wallOrigin = new(body.center.x, ledgeY - WallProbeDepth);
        float wallDistance = body.extents.x + reach + WallProbeDepth;
        if (!CastClosest(wallOrigin, Vector2.right * dir, wallDistance, out RaycastHit2D wall)) return false;
        if (wall.distance <= 0f || Mathf.Abs(wall.normal.x) < wallNormalThreshold) return false;

        Vector2 standSize = (Vector2)body.size * clearanceScale;
        Vector2 standCenter = new(wall.point.x + dir * (body.extents.x + Skin), ledgeY + body.extents.y + Skin);
        lastStandCenter = standCenter;
        lastStandSize = standSize;

        if (Physics2D.OverlapBox(standCenter, standSize, 0f, filter, overlapHits) > 0) return false;

        corner = new Vector2(wall.point.x, ledgeY);
        lastFound = true;
        lastCorner = corner;
        return true;
    }

    private bool CastClosest(Vector2 origin, Vector2 direction, float distance, out RaycastHit2D closest)
    {
        closest = default;

        int count = Physics2D.Raycast(origin, direction, filter, rayHits, distance);
        if (count == 0) return false;

        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (rayHits[i].distance < best)
            {
                best = rayHits[i].distance;
                closest = rayHits[i];
            }
        }

        return IsClimbable(closest.collider);
    }

    private static bool IsClimbable(Collider2D col)
    {
        if (col == null) return false;
        if (col.TryGetComponent(out AirOnlyCollisionPlatform _)) return false;
        if (col.TryGetComponent(out DropThroughPlatform _)) return false;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!hasLastQuery || lastDir == 0) return;

        float front = lastDir > 0 ? lastBody.max.x : lastBody.min.x;
        float x = front + lastDir * reach;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(x, lastBody.max.y + grabZoneTop), new Vector3(x, lastBody.max.y + grabZoneBottom));

        if (lastStandSize != Vector2.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(lastStandCenter, lastStandSize);
        }

        if (lastFound)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(lastCorner, 0.06f);
        }
    }
}