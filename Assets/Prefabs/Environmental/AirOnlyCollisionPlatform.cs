using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider2D))]
public class AirOnlyCollisionPlatform : MonoBehaviour
{
    [SerializeField] private Vector2 solidLocalDirection = Vector2.up;
    [SerializeField] private float ignoreDuration = 0.75f;
    [SerializeField] private LayerMask enemyLayer;

    private Collider2D platformCollider;
    private BoxCollider2D boxCollider;
    private Player cachedPlayer;
    private Collider2D[] playerColliders;
    private Coroutine ignoreRoutine;

    private void Awake()
    {
        platformCollider = GetComponent<Collider2D>();
        boxCollider = platformCollider as BoxCollider2D;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
        {
            Physics2D.IgnoreCollision(collision.collider, platformCollider, true);
        }
    }

    private void FixedUpdate()
    {
        if (cachedPlayer == null)
        {
            cachedPlayer = FindFirstObjectByType<Player>();
            if (cachedPlayer == null) return;
            playerColliders = cachedPlayer.GetComponentsInChildren<Collider2D>(true);
        }

        bool wrongSide = cachedPlayer.Controller.IsGrounded || cachedPlayer.Controller.IsDashing || !IsNearSolidFace();

        if (wrongSide && ignoreRoutine == null)
        {
            IgnoreCollisionFor(playerColliders, ignoreDuration);
        }
    }

    public void IgnoreCollisionFor(Collider2D[] colliders, float duration)
    {
        if (ignoreRoutine != null) StopCoroutine(ignoreRoutine);
        ignoreRoutine = StartCoroutine(IgnoreCollisionRoutine(colliders, duration));
    }

    private IEnumerator IgnoreCollisionRoutine(Collider2D[] colliders, float duration)
    {
        SetIgnore(colliders, true);
        yield return new WaitForSeconds(duration);
        SetIgnore(colliders, false);
        ignoreRoutine = null;
    }

    private void SetIgnore(Collider2D[] colliders, bool ignore)
    {
        foreach (var col in colliders)
        {
            if (col != null) Physics2D.IgnoreCollision(col, platformCollider, ignore);
        }
    }

    private bool IsNearSolidFace()
    {
        Vector2 localOffset = transform.InverseTransformPoint(cachedPlayer.transform.position);

        Vector2 halfExtents = boxCollider != null
            ? Vector2.Scale(boxCollider.size, transform.lossyScale) * 0.5f
            : Vector2.one * 0.5f;

        Vector2 scaledOffset = new Vector2(
            halfExtents.x > 0f ? localOffset.x / halfExtents.x : 0f,
            halfExtents.y > 0f ? localOffset.y / halfExtents.y : 0f
        );

        Vector2 dominantAxis = Mathf.Abs(scaledOffset.x) > Mathf.Abs(scaledOffset.y)
            ? new Vector2(Mathf.Sign(scaledOffset.x), 0f)
            : new Vector2(0f, Mathf.Sign(scaledOffset.y));

        return Vector2.Dot(dominantAxis, solidLocalDirection.normalized) > 0.5f;
    }

    public bool AllowsSolidContactFrom(Vector2 hitNormal)
    {
        Vector2 worldSolidDir = transform.TransformDirection(solidLocalDirection.normalized);
        return Vector2.Dot(hitNormal.normalized, worldSolidDir) > 0.7f;
    }
}