using UnityEngine;

[DefaultExecutionOrder(1000)]
public class ParallaxTilemap : MonoBehaviour
{
    [SerializeField] private float multiplier = 0.3f;
    [SerializeField] private float referenceOffsetX;
    [SerializeField, Min(0f)] private float pixelsPerUnit = 0f;

    private Vector3 startLocalPosition;
    private bool initialised;
    private Camera cam;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        initialised = true;
    }

    private void OnDisable()
    {
        if (initialised) transform.localPosition = startLocalPosition;
    }

    private Vector3 PlacedWorldPosition
    {
        get
        {
            if (!initialised) return transform.position;
            return transform.parent != null ? transform.parent.TransformPoint(startLocalPosition) : startLocalPosition;
        }
    }

    private float ReferenceX => PlacedWorldPosition.x + referenceOffsetX;

    private void LateUpdate()
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null) return;

        float offset = (cam.transform.position.x - ReferenceX) * multiplier;

        if (pixelsPerUnit > 0f)
            offset = Mathf.Round(offset * pixelsPerUnit) / pixelsPerUnit;

        Vector3 localOffset = new Vector3(offset, 0f, 0f);
        if (transform.parent != null) localOffset = transform.parent.InverseTransformVector(localOffset);

        transform.localPosition = startLocalPosition + localOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 placed = PlacedWorldPosition;
        Vector3 point = new Vector3(placed.x + referenceOffsetX, placed.y, placed.z);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(point + Vector3.down * 5f, point + Vector3.up * 5f);
        Gizmos.DrawWireSphere(point, 0.3f);
    }
}