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

    private Camera _cam;
    private Vector3 _velocity = Vector3.zero;
    private float _verticalCamTarget;
    private bool _verticalTargetInitialized;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
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

        transform.position = new Vector3(
            target.position.x + horizontalOffset,
            _verticalCamTarget,
            transform.position.z
        );

        _verticalTargetInitialized = true;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (!_verticalTargetInitialized)
        {
            SnapToTarget();
            return;
        }

        float targetX = target.position.x + horizontalOffset;
        float currentViewportY = ViewportYOf(target.position.y);

        if (currentViewportY > upperThreshold)
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