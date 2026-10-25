using UnityEngine;

namespace Enemies
{
    public abstract class EnemyObservationSO : ScriptableObject
    {
        public abstract void Tick(EnemyContext ctx, float deltaTime);
    }
    
    public abstract class EnemyMovementSO : ScriptableObject
    {
        public abstract void Tick(EnemyContext ctx, float deltaTime);
    }
 
    public abstract class EnemyAttackSO : ScriptableObject
    {
        [SerializeField] protected float range = 1f;
        [SerializeField] protected float cooldownDuration = 3f;

        [Header("Vertical Reach")]
        [SerializeField] protected bool useVerticalBand = false;
        [SerializeField] protected float bottomMargin = 0.1f;
        [SerializeField] protected float topMargin = 0.1f;
        [SerializeField] protected bool debugVerticalBand = false;

        [Header("Boundary Clearance")]
        [SerializeField] protected bool keepClearOfBoundary = false;
        [SerializeField, Min(0f)] protected float boundaryClearance = 2f;
        [SerializeField, Min(0.1f)] protected float repositionSpeed = 4f;
        [SerializeField, Min(0.1f)] protected float repositionTimeout = 3f;
        [SerializeField] protected bool attackIfBlocked = false;
        [SerializeField] protected string repositionWindupTrigger = "MoveWindup";
        [SerializeField] protected string repositionWindupState = "MoveWindup";
        [SerializeField] protected string repositionWinddownTrigger = "MoveWinddown";
        [SerializeField] protected string repositionWinddownState = "MoveWinddown";
        [SerializeField, Min(0.1f)] protected float repositionPhaseTimeout = 1.5f;
        [SerializeField] protected bool debugBoundaryClearance = false;

        public float Range => range;
        public float CooldownDuration => cooldownDuration;
        public float RepositionSpeed => repositionSpeed;
        public float RepositionTimeout => repositionTimeout;
        public bool AttackIfBlocked => attackIfBlocked;
        public string RepositionWindupTrigger => repositionWindupTrigger;
        public string RepositionWindupState => repositionWindupState;
        public string RepositionWinddownTrigger => repositionWinddownTrigger;
        public string RepositionWinddownState => repositionWinddownState;
        public float RepositionPhaseTimeout => repositionPhaseTimeout;

        public bool NeedsBoundaryReposition(EnemyContext ctx, out float targetCenterX, out int direction)
        {
            targetCenterX = 0f;
            direction = 0;
            if (!keepClearOfBoundary || ctx.BossBounds == null) return false;

            Bounds arena = ctx.BossBounds.WorldBounds;
            Bounds body = EnemyTerrainProbe.GetBodyBounds(ctx);

            float minCenter = arena.min.x + boundaryClearance + body.extents.x;
            float maxCenter = arena.max.x - boundaryClearance - body.extents.x;

            if (debugBoundaryClearance)
            {
                Debug.DrawLine(new Vector3(arena.min.x + boundaryClearance, body.min.y), new Vector3(arena.min.x + boundaryClearance, body.max.y), Color.cyan, 1f);
                Debug.DrawLine(new Vector3(arena.max.x - boundaryClearance, body.min.y), new Vector3(arena.max.x - boundaryClearance, body.max.y), Color.cyan, 1f);
            }

            if (minCenter > maxCenter)
            {
                minCenter = maxCenter = arena.center.x;
            }

            if (body.center.x < minCenter - 0.01f) targetCenterX = minCenter;
            else if (body.center.x > maxCenter + 0.01f) targetCenterX = maxCenter;
            else return false;

            direction = targetCenterX > body.center.x ? 1 : -1;
            return true;
        }
        
        public abstract void Begin(EnemyContext ctx);
        public abstract bool IsFinished(EnemyContext ctx);
        public virtual bool CanUse(EnemyContext ctx) => true;

        public bool IsInVerticalBand(EnemyContext ctx)
        {
            if (!useVerticalBand || ctx.Target == null) return true;

            Bounds body = EnemyTerrainProbe.GetBodyBounds(ctx);
            float bottom = body.min.y + bottomMargin;
            float top = body.max.y - topMargin;
            if (top - bottom < 0.1f)
            {
                float mid = body.center.y;
                bottom = mid - 0.05f;
                top = mid + 0.05f;
            }

            Bounds target = GetTargetBounds(ctx.Target);
            bool inBand = target.max.y >= bottom && target.min.y <= top;

            if (debugVerticalBand)
            {
                Color color = inBand ? Color.green : Color.red;
                float left = body.min.x - range;
                float right = body.max.x + range;
                Debug.DrawLine(new Vector3(left, bottom), new Vector3(right, bottom), color);
                Debug.DrawLine(new Vector3(left, top), new Vector3(right, top), color);
            }

            return inBand;
        }

        private static Bounds GetTargetBounds(Transform target)
        {
            Player player = target.GetComponentInParent<Player>();
            if (player != null && player.Controller != null) return player.Controller.BodyBounds;

            Collider2D col = target.GetComponentInChildren<Collider2D>();
            return col != null ? col.bounds : new Bounds(target.position, Vector3.zero);
        }
    }
}