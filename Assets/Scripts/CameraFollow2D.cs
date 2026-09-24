using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Horizontal Follow")]
    [SerializeField] private float horizontalSmoothTime = 0.15f;
    [SerializeField] private float horizontalOffset = 0f;

    [Header("Vertical Deadzone (viewport space, 0 = bottom, 1 = top)")]
    [SerializeField] private float restingViewportY = 0.35f; // where player sits relative to view
    [SerializeField] private float upperThreshold = 0.7f; // when camera shifts up
    [SerializeField] private float lowerThreshold = 0.15f; // when camera shifts down
    [SerializeField] private float verticalSmoothTime = 0.12f;

    [Header("Look Ahead")]
    [Tooltip("How far (world units) the camera shifts toward the direction the target is moving.")]
    [SerializeField] private float lookAheadDistance = 2f;
    [Tooltip("How smoothly the look-ahead offset eases in when moving and back to center when stopped.")]
    [SerializeField] private float lookAheadSmoothTime = 0.3f;
    [Tooltip("Minimum horizontal speed (world units/sec) before look-ahead engages at all — filters out standing-still jitter.")]
    [SerializeField] private float lookAheadMoveThreshold = 0.5f;
    [Tooltip("Smooths the raw frame-to-frame velocity estimate before it's compared against the threshold — filters out single-frame noise (e.g. physics friction jitter while standing still) that would otherwise briefly read as movement.")]
    [SerializeField] private float velocityNoiseSmoothTime = 0.1f;

    [Header("Reveal Zoom")]
    [Tooltip("How long the zoom-out/zoom-back transitions take.")]
    [SerializeField] private float revealTransitionDuration = 0.75f;
    [Tooltip("Extra breathing room around the revealed bounds — 1 = exact fit, 1.1 = 10% padding.")]
    [SerializeField] private float revealZoomPadding = 1.1f;

    private Camera _cam;
    private Vector3 _velocity = Vector3.zero;
    private float _verticalCamTarget;
    private bool _verticalTargetInitialized;

    private float _lastTargetPosX;
    private float _smoothedVelocityX;
    private float _velocitySmoothingVelocity;
    private float _currentLookAhead;
    private float _lookAheadVelocity;

    private float _baseOrthographicSize;
    private float _preRevealVerticalCamTarget;
    private bool _isRevealing;
    private bool _justHandedOffFromReveal;
    private Coroutine _revealRoutine;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _baseOrthographicSize = _cam.orthographicSize;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        _verticalTargetInitialized = false;
    }

    public void SnapToTarget()
    {
        if (target == null) return;

        _verticalCamTarget = WorldYForViewportY(target.position.y, restingViewportY);

        _lastTargetPosX = target.position.x;
        _smoothedVelocityX = 0f;
        _velocitySmoothingVelocity = 0f;
        _currentLookAhead = 0f;
        _lookAheadVelocity = 0f;

        transform.position = new Vector3(
            target.position.x + horizontalOffset,
            _verticalCamTarget,
            transform.position.z
        );

        _verticalTargetInitialized = true;
    }

    // Zooms/pans out just far enough to fit `bounds` fully in view, holds for
    // holdSeconds, then transitions back to normal zoom and resumes following target.
    // Call this from a trigger volume, passing the bounds of whatever area (e.g. a
    // second, separate collider) should be revealed. onRevealComplete, if given, fires
    // once the whole sequence (both transitions + hold) has finished.
    public void RevealBounds(Bounds bounds, float holdSeconds, System.Action onRevealComplete = null)
    {
        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(RevealBoundsRoutine(bounds, holdSeconds, onRevealComplete));
    }

    // Zooms/pans out to fit `bounds` and STAYS there indefinitely — no auto-return.
    // Call ReturnToNormalFollow() later (e.g. from GameManager) whenever it's actually
    // time to hand the camera back.
    public void LockToBounds(Bounds bounds)
    {
        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(LockToBoundsRoutine(bounds));
    }

    // Releases a LockToBounds (or interrupts an in-progress RevealBounds) and returns
    // to normal follow. onReturnComplete, if given, fires once fully back to normal.
    public void ReturnToNormalFollow(System.Action onReturnComplete = null)
    {
        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(ReturnToNormalFollowRoutine(onReturnComplete));
    }

    // True while a reveal/lock (zoom-out) is in progress or holding — useful if other
    // systems (UI, input) want to know the camera isn't in its normal follow state.
    public bool IsRevealing => _isRevealing;

    private IEnumerator RevealBoundsRoutine(Bounds bounds, float holdSeconds, System.Action onRevealComplete)
    {
        _isRevealing = true;
        _preRevealVerticalCamTarget = _verticalCamTarget;

        yield return TransitionToBoundsInternal(bounds);

        yield return new WaitForSeconds(holdSeconds);

        yield return HandOffAndZoomBack();

        _revealRoutine = null;

        onRevealComplete?.Invoke();
    }

    private IEnumerator LockToBoundsRoutine(Bounds bounds)
    {
        _isRevealing = true;
        _preRevealVerticalCamTarget = _verticalCamTarget;

        yield return TransitionToBoundsInternal(bounds);

        _revealRoutine = null;
        // Deliberately stops here — _isRevealing stays true, camera stays locked on
        // bounds, until ReturnToNormalFollow() is called externally.
    }

    private IEnumerator ReturnToNormalFollowRoutine(System.Action onReturnComplete)
    {
        yield return HandOffAndZoomBack();

        _revealRoutine = null;

        onReturnComplete?.Invoke();
    }

    private IEnumerator TransitionToBoundsInternal(Bounds bounds)
    {
        float aspect = _cam.aspect;
        float requiredSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * revealZoomPadding;
        Vector3 revealPos = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);

        yield return TransitionCamera(transform.position, revealPos, _cam.orthographicSize, requiredSize, revealTransitionDuration);
    }

    // No manual return-position transition — position is handed entirely to normal
    // follow, which pans back to the player using its own existing logic. Zoom still
    // needs its own transition here since normal follow never touches orthographic
    // size, only position.
    private IEnumerator HandOffAndZoomBack()
    {
        _lastTargetPosX = target != null ? target.position.x : transform.position.x;
        _smoothedVelocityX = 0f;
        _velocitySmoothingVelocity = 0f;
        _currentLookAhead = 0f;
        _lookAheadVelocity = 0f;
        _velocity = Vector3.zero;
        _justHandedOffFromReveal = true;

        _isRevealing = false;

        yield return ZoomTo(_baseOrthographicSize, revealTransitionDuration);
    }

    private IEnumerator ZoomTo(float toSize, float duration)
    {
        if (duration <= 0f)
        {
            _cam.orthographicSize = toSize;
            yield break;
        }

        float fromSize = _cam.orthographicSize;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = p * p * (3f - 2f * p); // smoothstep
            _cam.orthographicSize = Mathf.Lerp(fromSize, toSize, eased);
            yield return null;
        }

        _cam.orthographicSize = toSize;
    }

    private IEnumerator TransitionCamera(Vector3 fromPos, Vector3 toPos, float fromSize, float toSize, float duration)
    {
        if (duration <= 0f)
        {
            transform.position = toPos;
            _cam.orthographicSize = toSize;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = p * p * (3f - 2f * p); // smoothstep

            transform.position = Vector3.Lerp(fromPos, toPos, eased);
            _cam.orthographicSize = Mathf.Lerp(fromSize, toSize, eased);

            yield return null;
        }

        transform.position = toPos;
        _cam.orthographicSize = toSize;
    }

    private void LateUpdate()
    {
        if (_isRevealing) return; // the reveal coroutine has full control of position/zoom right now

        if (target == null) return;

        if (!_verticalTargetInitialized)
        {
            SnapToTarget();
            return;
        }

        float rawX = target.position.x;

        // Infer movement directly from the target's own position delta rather than
        // referencing PlayerController — keeps this camera generic for any target.
        float rawVelocityX = Time.deltaTime > 0f ? (rawX - _lastTargetPosX) / Time.deltaTime : 0f;
        _lastTargetPosX = rawX;

        // Smooth the estimate itself before checking it against the threshold — a raw
        // single-frame delta is noisy enough (physics friction jitter, etc.) to briefly
        // read as movement even while genuinely standing still.
        _smoothedVelocityX = Mathf.SmoothDamp(_smoothedVelocityX, rawVelocityX, ref _velocitySmoothingVelocity, velocityNoiseSmoothTime);

        // No recentering: if not currently moving, the desired offset is just wherever
        // it already is, so SmoothDamp holds it in place rather than pulling back to 0.
        // It only actually changes target when there's genuine new movement.
        float desiredLookAhead = _currentLookAhead;
        if (Mathf.Abs(_smoothedVelocityX) > lookAheadMoveThreshold)
        {
            desiredLookAhead = Mathf.Sign(_smoothedVelocityX) * lookAheadDistance;
        }

        _currentLookAhead = Mathf.SmoothDamp(_currentLookAhead, desiredLookAhead, ref _lookAheadVelocity, lookAheadSmoothTime);

        float targetX = rawX + horizontalOffset + _currentLookAhead;
        float currentViewportY = ViewportYOf(target.position.y);

        if (_justHandedOffFromReveal)
        {
            // Restore exactly where the camera's vertical target legitimately was
            // before the reveal interrupted it — not a freshly recomputed resting
            // position, which could differ from wherever deadzone history had actually
            // left it (e.g. from an earlier jump) and show up as a small correction.
            _verticalCamTarget = _preRevealVerticalCamTarget;
            _justHandedOffFromReveal = false;
        }
        else if (currentViewportY > upperThreshold)
        {
            _verticalCamTarget = WorldYForViewportY(target.position.y, upperThreshold);
        }
        else if (currentViewportY < lowerThreshold)
        {
            _verticalCamTarget = WorldYForViewportY(target.position.y, lowerThreshold);
        }

        Vector3 desiredPosition = new Vector3(targetX, _verticalCamTarget, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity,
            Mathf.Max(horizontalSmoothTime, verticalSmoothTime));
    }

    private float ViewportYOf(float worldY)
    {
        float halfHeight = _cam.orthographicSize;
        return 0.5f + (worldY - transform.position.y) / (2f * halfHeight);
    }

    private float WorldYForViewportY(float worldY, float viewportY)
    {
        float halfHeight = _cam.orthographicSize;
        return worldY - halfHeight * (2f * viewportY - 1f);
    }
}