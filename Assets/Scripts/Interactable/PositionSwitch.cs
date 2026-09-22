using System.Collections;
using UnityEngine;

public class PositionSwitch : MonoBehaviour, IOnOff, IMovementIndicator
{
    [Header("Positions")]
    [SerializeField] private Transform movingPart;
    [SerializeField] private Transform onPosition;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveDuration = 0.3f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Must be on the SAME object as Moving Part (or its own object, if Moving Part is left empty). A Rigidbody2D only governs colliders on its own object or descendants — never an ancestor — so it can't live deeper in the hierarchy than the thing it needs to move.")]
    [SerializeField] private Rigidbody2D moverRigidbody;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string isMovingParameter = "IsMoving";

    [Header("Debug")]
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color onGizmoColor = Color.green;
    [SerializeField] private Color offGizmoColor = Color.yellow;

    private Vector3 offPosition;
    private bool offPositionCaptured;
    private Coroutine moveRoutine;
    private int? isMovingParamHash;

    public bool IsOn { get; private set; }
    public bool IsMoving => moveRoutine != null;

    private void Awake()
    {
        if (doorCollider == null) doorCollider = GetComponentInChildren<Collider2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        isMovingParamHash ??= Animator.StringToHash(isMovingParameter);

        if (moverRigidbody == null) moverRigidbody = Mover().GetComponent<Rigidbody2D>();

        if (moverRigidbody != null)
        {
            if (moverRigidbody.transform != Mover())
            {
                Debug.LogWarning($"{name}: Mover Rigidbody is on '{moverRigidbody.name}', not on the moving object ('{Mover().name}') itself. A Rigidbody2D only governs colliders on its own object or below it, never above — move the component onto '{Mover().name}' directly, or promote that object's collider setup so the rigidbody sits at the right level. Ignoring it for now.", this);
                moverRigidbody = null;
            }
            else
            {
                moverRigidbody.bodyType = RigidbodyType2D.Kinematic;
                moverRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        if (moverRigidbody == null)
        {
            Debug.LogWarning($"{name}: no Rigidbody2D on '{Mover().name}' — movement will fall back to plain Transform positioning, which can cause jitter/drag for anything standing on this.", this);
        }

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
            SetMoverPosition(target.Value);
            moveRoutine = null;
            SetAnimatorMoving(false);
            return;
        }

        SetAnimatorMoving(true);
        moveRoutine = StartCoroutine(MoveRoutine(target.Value));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        Vector3 start = Mover().position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.fixedDeltaTime / moveDuration;
            float eased = Mathf.Clamp01(moveCurve.Evaluate(Mathf.Clamp01(t)));
            SetMoverPosition(Vector3.LerpUnclamped(start, targetPosition, eased));
            yield return new WaitForFixedUpdate();
        }

        SetMoverPosition(targetPosition);
        moveRoutine = null;
        SetAnimatorMoving(false);
    }

    private void SetMoverPosition(Vector3 position)
    {
        if (moverRigidbody != null)
            moverRigidbody.MovePosition(position);
        else
            Mover().position = position;
    }

    private void SetAnimatorMoving(bool moving)
    {
        if (animator == null) return;

        isMovingParamHash ??= Animator.StringToHash(isMovingParameter);
        animator.SetBool(isMovingParamHash.Value, moving);
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