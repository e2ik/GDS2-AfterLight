using UnityEngine;

namespace Enemies
{
    public class EyeFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private bool autoFindPlayerByTag = true;
        [SerializeField] private string playerTag = "Player";

        [Header("Movement")]
        [SerializeField] private float maxOffset = 0.1f;
        [SerializeField] private float followSpeed = 8f;

        [Header("Range")]
        [SerializeField] private float maxTrackDistance = 10f;

        private Vector3 restLocalPosition;
        private bool hasRestPosition;

        private void Awake()
        {
            restLocalPosition = transform.localPosition;
            hasRestPosition = true;
        }

        private void OnEnable()
        {
            if (!hasRestPosition)
            {
                restLocalPosition = transform.localPosition;
                hasRestPosition = true;
            }

            TryAcquireTarget();
        }

        private void OnDisable()
        {
            transform.localPosition = restLocalPosition;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        private void TryAcquireTarget()
        {
            if (target != null) return;

            if (GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                target = GameManager.Instance.Player.transform;
            }
            else if (autoFindPlayerByTag)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObj != null)
                {
                    target = playerObj.transform;
                }
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, restLocalPosition, followSpeed * Time.deltaTime);
                return;
            }

            Vector3 toTarget = target.position - transform.parent.position;
            float distance = toTarget.magnitude;

            Vector3 desiredLocalPos;

            if (distance > maxTrackDistance)
            {
                desiredLocalPos = restLocalPosition;
            }
            else
            {
                Vector2 dir = toTarget.normalized;
                Vector2 clampedOffset = dir * Mathf.Min(maxOffset, distance);
                desiredLocalPos = restLocalPosition + (Vector3)clampedOffset;
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, desiredLocalPos, followSpeed * Time.deltaTime);
        }
    }
}