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
    [SerializeField] private float restingViewportY = 0.35f;
    [SerializeField] private float upperThreshold = 0.7f;
    [SerializeField] private float lowerThreshold = 0.15f;
    [SerializeField] private float verticalSmoothTime = 0.12f;

    [Header("Dialogue")]
    [SerializeField] private bool centerDuringDialogue = true;
    [SerializeField] private float dialogueViewportY = 0.5f;
    [SerializeField] private bool zoomInOnConversation = true;
    [SerializeField] private float conversationZoomPadding = 1.5f;
    [SerializeField] private float conversationMinSize = 2.5f;
    [SerializeField] private float conversationZoomSmoothTime = 0.35f;
    [SerializeField] private float conversationFocusOffsetY = 0f;
    [SerializeField] private bool canZoomDuringLock = false;

    [Header("Look Ahead")]
    [SerializeField] private float lookAheadDistance = 2f;
    [SerializeField] private float lookAheadSmoothTime = 0.3f;
    [SerializeField] private float lookAheadMoveThreshold = 0.5f;
    [SerializeField] private float velocityNoiseSmoothTime = 0.1f;

    [Header("Reveal Zoom")]
    [SerializeField] private float revealTransitionDuration = 0.75f;
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
    private bool _wasInDialogue;
    private bool _conversationZoomActive;
    private bool _returningFromConversationZoom;
    private float _zoomVelocity;
    private float _preConversationVerticalCamTarget;
    private bool _isLocked;
    private bool _returningToLock;
    private Vector3 _lockedPosition;
    private float _lockedSize;

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

    public void SettleToRest()
    {
        if (target == null) return;

        if (!_verticalTargetInitialized)
        {
            SnapToTarget();
            return;
        }

        _currentLookAhead = Mathf.Abs(_currentLookAhead) > 0.01f
            ? Mathf.Sign(_currentLookAhead) * lookAheadDistance
            : 0f;

        _lastTargetPosX = target.position.x;
        _smoothedVelocityX = 0f;
        _velocitySmoothingVelocity = 0f;
        _lookAheadVelocity = 0f;
        _velocity = Vector3.zero;

        transform.position = new Vector3(
            target.position.x + horizontalOffset + _currentLookAhead,
            _verticalCamTarget,
            transform.position.z
        );
    }

    public void RevealBounds(Bounds bounds, float holdSeconds, System.Action onRevealComplete = null)
    {
        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(RevealBoundsRoutine(bounds, holdSeconds, onRevealComplete));
    }

    public void LockToBounds(Bounds bounds)
    {
        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(LockToBoundsRoutine(bounds));
    }

    public void ReturnToNormalFollow(System.Action onReturnComplete = null)
    {
        if (_revealRoutine != null) StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(ReturnToNormalFollowRoutine(onReturnComplete));
    }

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

        _lockedPosition = transform.position;
        _lockedSize = _cam.orthographicSize;
        _isLocked = true;

        _revealRoutine = null;
    }

    private IEnumerator ReturnToNormalFollowRoutine(System.Action onReturnComplete)
    {
        yield return HandOffAndZoomBack();

        _revealRoutine = null;

        onReturnComplete?.Invoke();
    }

    private IEnumerator TransitionToBoundsInternal(Bounds bounds)
    {
        _conversationZoomActive = false;
        _returningFromConversationZoom = false;
        _isLocked = false;
        _returningToLock = false;

        float aspect = _cam.aspect;
        float requiredSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * revealZoomPadding;
        Vector3 revealPos = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);

        yield return TransitionCamera(transform.position, revealPos, _cam.orthographicSize, requiredSize, revealTransitionDuration);
    }

    private IEnumerator HandOffAndZoomBack()
    {
        _lastTargetPosX = target != null ? target.position.x : transform.position.x;
        _smoothedVelocityX = 0f;
        _velocitySmoothingVelocity = 0f;
        _currentLookAhead = 0f;
        _lookAheadVelocity = 0f;
        _velocity = Vector3.zero;
        _justHandedOffFromReveal = true;

        _isLocked = false;
        _returningToLock = false;
        _conversationZoomActive = false;
        _zoomVelocity = 0f;

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
            float eased = p * p * (3f - 2f * p);
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
            float eased = p * p * (3f - 2f * p);

            transform.position = Vector3.Lerp(fromPos, toPos, eased);
            _cam.orthographicSize = Mathf.Lerp(fromSize, toSize, eased);

            yield return null;
        }

        transform.position = toPos;
        _cam.orthographicSize = toSize;
    }

    private void LateUpdate()
    {
        if (_isRevealing)
        {
            if (_isLocked && canZoomDuringLock && target != null)
                UpdateLockedConversation();
            return;
        }

        if (target == null) return;

        if (!_verticalTargetInitialized)
        {
            SnapToTarget();
            return;
        }

        Transform conversationTarget = GetConversationTarget();
        if (conversationTarget != null)
        {
            UpdateConversationZoom(conversationTarget);
            return;
        }

        if (_conversationZoomActive)
        {
            _conversationZoomActive = false;
            _returningFromConversationZoom = true;
            _verticalCamTarget = _preConversationVerticalCamTarget;
            _velocity = Vector3.zero;
            _zoomVelocity = 0f;
            _wasInDialogue = false;
        }

        if (_returningFromConversationZoom)
        {
            _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, _baseOrthographicSize, ref _zoomVelocity, conversationZoomSmoothTime);
            if (Mathf.Abs(_cam.orthographicSize - _baseOrthographicSize) < 0.01f)
            {
                _cam.orthographicSize = _baseOrthographicSize;
                _zoomVelocity = 0f;
                _returningFromConversationZoom = false;
            }
        }

        float rawX = target.position.x;

        float rawVelocityX = Time.deltaTime > 0f ? (rawX - _lastTargetPosX) / Time.deltaTime : 0f;
        _lastTargetPosX = rawX;

        _smoothedVelocityX = Mathf.SmoothDamp(_smoothedVelocityX, rawVelocityX, ref _velocitySmoothingVelocity, velocityNoiseSmoothTime);

        float desiredLookAhead = _currentLookAhead;
        if (Mathf.Abs(_smoothedVelocityX) > lookAheadMoveThreshold)
        {
            desiredLookAhead = Mathf.Sign(_smoothedVelocityX) * lookAheadDistance;
        }

        _currentLookAhead = Mathf.SmoothDamp(_currentLookAhead, desiredLookAhead, ref _lookAheadVelocity, lookAheadSmoothTime);

        float targetX = rawX + horizontalOffset + _currentLookAhead;
        float currentViewportY = ViewportYOf(target.position.y);

        bool inDialogue = centerDuringDialogue
            && DialogueManager.Instance != null
            && DialogueManager.Instance.IsDialogueActive;

        if (inDialogue)
        {
            _verticalCamTarget = WorldYForViewportY(target.position.y, dialogueViewportY);
            _justHandedOffFromReveal = false;
        }
        else if (_wasInDialogue)
        {
            _verticalCamTarget = WorldYForViewportY(target.position.y, restingViewportY);
        }
        else if (_justHandedOffFromReveal)
        {
            _verticalCamTarget = _preRevealVerticalCamTarget;
            _justHandedOffFromReveal = false;
        }
        else if (!_returningFromConversationZoom && currentViewportY > upperThreshold)
        {
            _verticalCamTarget = WorldYForViewportY(target.position.y, upperThreshold);
        }
        else if (!_returningFromConversationZoom && currentViewportY < lowerThreshold)
        {
            _verticalCamTarget = WorldYForViewportY(target.position.y, lowerThreshold);
        }

        _wasInDialogue = inDialogue;

        float followSmoothTime = _returningFromConversationZoom
            ? conversationZoomSmoothTime
            : Mathf.Max(horizontalSmoothTime, verticalSmoothTime);

        Vector3 desiredPosition = new Vector3(targetX, _verticalCamTarget, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, followSmoothTime);
    }

    private void UpdateLockedConversation()
    {
        Transform conversationTarget = GetConversationTarget();
        if (conversationTarget != null)
        {
            _returningToLock = false;
            UpdateConversationZoom(conversationTarget);
            return;
        }

        if (_conversationZoomActive)
        {
            _conversationZoomActive = false;
            _returningToLock = true;
            _velocity = Vector3.zero;
            _zoomVelocity = 0f;
        }

        if (!_returningToLock) return;

        transform.position = Vector3.SmoothDamp(transform.position, _lockedPosition, ref _velocity, conversationZoomSmoothTime);
        _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, _lockedSize, ref _zoomVelocity, conversationZoomSmoothTime);

        if ((transform.position - _lockedPosition).sqrMagnitude < 0.0001f && Mathf.Abs(_cam.orthographicSize - _lockedSize) < 0.01f)
        {
            transform.position = _lockedPosition;
            _cam.orthographicSize = _lockedSize;
            _velocity = Vector3.zero;
            _zoomVelocity = 0f;
            _returningToLock = false;
        }
    }

    private Transform GetConversationTarget()
    {
        if (!zoomInOnConversation || DialogueManager.Instance == null || !DialogueManager.Instance.IsDialogueActive)
            return null;

        NPCDialogue npc = DialogueManager.Instance.CurrentNPC;
        return npc != null ? npc.transform : null;
    }

    private void UpdateConversationZoom(Transform other)
    {
        if (!_conversationZoomActive)
        {
            _preConversationVerticalCamTarget = _verticalCamTarget;
            _velocity = Vector3.zero;
            _zoomVelocity = 0f;
        }

        Bounds focus = GetFocusBounds(target);
        focus.Encapsulate(GetFocusBounds(other));

        float desiredSize = Mathf.Max(focus.extents.y, focus.extents.x / _cam.aspect) * conversationZoomPadding;
        desiredSize = Mathf.Clamp(desiredSize, conversationMinSize, _baseOrthographicSize);

        Vector3 desiredPosition = new Vector3(focus.center.x, focus.center.y + conversationFocusOffsetY, transform.position.z);

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, conversationZoomSmoothTime);
        _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, desiredSize, ref _zoomVelocity, conversationZoomSmoothTime);

        _lastTargetPosX = target.position.x;
        _smoothedVelocityX = 0f;
        _velocitySmoothingVelocity = 0f;
        _currentLookAhead = 0f;
        _lookAheadVelocity = 0f;

        _conversationZoomActive = true;
        _returningFromConversationZoom = false;
    }

    private Bounds GetFocusBounds(Transform t)
    {
        SpriteRenderer sr = t.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.bounds : new Bounds(t.position, Vector3.zero);
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