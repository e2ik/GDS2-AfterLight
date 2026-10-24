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

        public float Range => range;
        public float CooldownDuration => cooldownDuration;
        
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