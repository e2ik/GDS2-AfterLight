using UnityEngine;

public class DropThroughPlatform : MonoBehaviour
{
    // :)
    [SerializeField] private bool isMovingPlatform;
    [SerializeField, Min(0f)] private float rideTolerance = 1f;
    [SerializeField] private bool climbable = false;
    [SerializeField] private bool droppable = true;
    [SerializeField] private bool undroppableDuringBossFight = false;

    private Vector2 lastPosition;
    private bool hasLastPosition;

    public bool IsMovingPlatform => isMovingPlatform;
    public bool IsClimbable => climbable;
    public bool IsDroppable => droppable && !(undroppableDuringBossFight && Enemies.BossTrigger.AnyBossEngaged);

    public void SetDroppable(bool value) => droppable = value;
    public Vector2 Velocity { get; private set; }

    public bool IsRiding(float riderVelocityY)
    {
        return isMovingPlatform && riderVelocityY - Velocity.y <= rideTolerance;
    }

    private void OnEnable()
    {
        hasLastPosition = false;
        Velocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (!isMovingPlatform) return;

        Vector2 position = transform.position;
        Velocity = hasLastPosition && Time.fixedDeltaTime > 0f ? (position - lastPosition) / Time.fixedDeltaTime : Vector2.zero;
        lastPosition = position;
        hasLastPosition = true;
    }
}