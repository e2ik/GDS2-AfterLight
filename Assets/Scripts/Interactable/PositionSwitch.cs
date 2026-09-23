using System.Collections;
using UnityEngine;

public class PositionSwitch : MonoBehaviour, IOnOff, IMovementIndicator
{
    [Header("Positions")]
    [SerializeField] private Transform movingPart;
    [SerializeField] private Transform onPosition;

    [Header("Secondary Moving Part (optional)")]
    [Tooltip("Optional second object that moves alongside the main one, using the same timing/curve. Never uses a Rigidbody2D — always moves via plain Transform. Leave empty to disable entirely.")]
    [SerializeField] private Transform secondaryMovingPart;
    [SerializeField] private Transform secondaryOnPosition;
    [SerializeField] private Animator secondaryAnimator;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveDuration = 0.3f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Must be on the SAME object as Moving Part (or its own object, if Moving Part is left empty). A Rigidbody2D only governs colliders on its own object or descendants — never an ancestor — so it can't live deeper in the hierarchy than the thing it needs to move.")]
    [SerializeField] private Rigidbody2D moverRigidbody;

    [Header("Animation")]
    [SerializeField] private Animator[] animators;
    [SerializeField] private string isMovingParameter = "IsMoving";

    [Header("Audio")]
    [Tooltip("Set the event on this to a looping sound (e.g. a motor hum). Played while moving, stopped the instant motion ends — not a one-shot.")]
    [SerializeField] private FMODUnity.StudioEventEmitter movementLoopEmitter;

    [Header("Debug")]
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color onGizmoColor = Color.green;
    [SerializeField] private Color offGizmoColor = Color.yellow;

    private Vector3 offPosition;
    private bool offPositionCaptured;
    private Vector3 secondaryOffPosition;
    private bool secondaryOffPositionCaptured;
    private Coroutine moveRoutine;
    private int? isMovingParamHash;

    public bool IsOn { get; private set; }
    public bool IsMoving => moveRoutine != null;

    private bool HasSecondary => secondaryMovingPart != null;

    private void OnDisable()
    {
        if (moveRoutine != null)
        {
            moveRoutine = null;
            NotifyMovingStateChanged(false);
        }
    }

    private void Awake()
    {
        if (doorCollider == null) doorCollider = GetComponentInChildren<Collider2D>();
        if (animators == null || animators.Length == 0) animators = GetComponentsInChildren<Animator>();
        if (HasSecondary && secondaryAnimator == null) secondaryAnimator = secondaryMovingPart.GetComponentInChildren<Animator>();
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
        if (!offPositionCaptured)
        {
            offPosition = Mover().position;
            offPositionCaptured = true;
        }

        if (HasSecondary && !secondaryOffPositionCaptured)
        {
            secondaryOffPosition = secondaryMovingPart.position;
            secondaryOffPositionCaptured = true;
        }
    }

    private Transform Mover() => movingPart != null ? movingPart : transform;

    public void SetOn(bool on)
    {
        CaptureOffPosition();

        IsOn = on;

        Vector3? target = on ? (onPosition != null ? onPosition.position : (Vector3?)null) : offPosition;
        if (target == null) return;

        Vector3? secondaryTarget = HasSecondary
            ? (on ? (secondaryOnPosition != null ? secondaryOnPosition.position : (Vector3?)null) : secondaryOffPosition)
            : null;

        if (moveRoutine != null) StopCoroutine(moveRoutine);

        if (moveDuration <= 0f || !isActiveAndEnabled)
        {
            SetMoverPosition(target.Value);
            if (secondaryTarget.HasValue) secondaryMovingPart.position = secondaryTarget.Value;
            moveRoutine = null;
            NotifyMovingStateChanged(false);
            return;
        }

        NotifyMovingStateChanged(true);
        moveRoutine = StartCoroutine(MoveRoutine(target.Value, secondaryTarget));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition, Vector3? secondaryTargetPosition)
    {
        Vector3 start = Mover().position;
        Vector3 secondaryStart = secondaryTargetPosition.HasValue ? secondaryMovingPart.position : default;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.fixedDeltaTime / moveDuration;
            float eased = Mathf.Clamp01(moveCurve.Evaluate(Mathf.Clamp01(t)));

            SetMoverPosition(Vector3.LerpUnclamped(start, targetPosition, eased));

            if (secondaryTargetPosition.HasValue)
                secondaryMovingPart.position = Vector3.LerpUnclamped(secondaryStart, secondaryTargetPosition.Value, eased);

            yield return new WaitForFixedUpdate();
        }

        SetMoverPosition(targetPosition);
        if (secondaryTargetPosition.HasValue) secondaryMovingPart.position = secondaryTargetPosition.Value;

        moveRoutine = null;
        NotifyMovingStateChanged(false);
    }

    private void SetMoverPosition(Vector3 position)
    {
        if (moverRigidbody != null)
            moverRigidbody.MovePosition(position);
        else
            Mover().position = position;
    }

    private void NotifyMovingStateChanged(bool moving)
    {
        isMovingParamHash ??= Animator.StringToHash(isMovingParameter);

        if (animators != null)
        {
            foreach (var anim in animators)
            {
                if (anim != null) anim.SetBool(isMovingParamHash.Value, moving);
            }
        }

        if (HasSecondary && secondaryAnimator != null) secondaryAnimator.SetBool(isMovingParamHash.Value, moving);

        if (movementLoopEmitter != null)
        {
            if (moving) movementLoopEmitter.Play();
            else movementLoopEmitter.Stop();
        }
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

        if (HasSecondary)
        {
            Vector3 secondaryOffPreview = secondaryOffPositionCaptured ? secondaryOffPosition : secondaryMovingPart.position;
            Gizmos.color = offGizmoColor;
            Gizmos.DrawWireSphere(secondaryOffPreview, 0.05f);

            if (secondaryOnPosition != null)
            {
                Gizmos.color = onGizmoColor;
                Gizmos.DrawWireSphere(secondaryOnPosition.position, 0.05f);
            }
        }
    }

    private static void DrawColliderPreview(Vector3 position, Vector3 localOffset, Vector3 size, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(position, 0.05f);
        Gizmos.DrawWireCube(position + localOffset, size);
    }
}