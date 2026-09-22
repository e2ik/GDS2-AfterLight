using System.Collections;
using UnityEngine;

public class PositionSwitch : MonoBehaviour, IOnOff
{
    [Header("Positions")]
    [SerializeField] private Transform movingPart;
    [SerializeField] private Transform onPosition;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveDuration = 0.3f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color onGizmoColor = Color.green;
    [SerializeField] private Color offGizmoColor = Color.yellow;

    private Vector3 offPosition;
    private bool offPositionCaptured;
    private Coroutine moveRoutine;

    public bool IsOn { get; private set; }

    private void Awake()
    {
        if (doorCollider == null) doorCollider = GetComponentInChildren<Collider2D>();
        CaptureOffPosition();
    }

    private void CaptureOffPosition()
    {
        if (offPositionCaptured) return;

        offPosition = Mover().position;
        offPositionCaptured = true;
    }

    private Transform Mover() => movingPart != null ? movingPart : transform;

    public void SetOn(bool on)
    {
        CaptureOffPosition();

        IsOn = on;

        Vector3? target = on ? (onPosition != null ? onPosition.position : (Vector3?)null) : offPosition;
        if (target == null) return;

        if (moveRoutine != null) StopCoroutine(moveRoutine);

        if (moveDuration <= 0f || !isActiveAndEnabled)
        {
            Mover().position = target.Value;
            moveRoutine = null;
            return;
        }

        moveRoutine = StartCoroutine(MoveRoutine(target.Value));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        Transform mover = Mover();
        Vector3 start = mover.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            float eased = Mathf.Clamp01(moveCurve.Evaluate(Mathf.Clamp01(t)));
            mover.position = Vector3.LerpUnclamped(start, targetPosition, eased);
            yield return null;
        }

        mover.position = targetPosition;
        moveRoutine = null;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Transform mover = Mover();
        Collider2D col = doorCollider != null ? doorCollider : GetComponentInChildren<Collider2D>();
        if (col == null) return;

        Vector3 localOffset = col.bounds.center - mover.position;
        Vector3 size = col.bounds.size;

        Vector3 offPreview = offPositionCaptured ? offPosition : mover.position;
        DrawColliderPreview(offPreview, localOffset, size, offGizmoColor);

        if (onPosition != null)
            DrawColliderPreview(onPosition.position, localOffset, size, onGizmoColor);
    }

    private static void DrawColliderPreview(Vector3 position, Vector3 localOffset, Vector3 size, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(position, 0.05f);
        Gizmos.DrawWireCube(position + localOffset, size);
    }
}