using UnityEngine;

namespace Enemies.ModuleScripts.Movement
{
    public enum ApproachPhase { None, Approach, Retreat, Done }

    public class ApproachRetreatState
    {
        public ApproachPhase Phase;
        public float Elapsed;
        public float RetreatStartX;
        public int RetreatDir;

        public bool Active => Phase == ApproachPhase.Approach || Phase == ApproachPhase.Retreat;
        public void Finish() { if (Active) Phase = ApproachPhase.Done; }
        public void Reset() => Phase = ApproachPhase.None;
    }

    [CreateAssetMenu(menuName = "Enemies/Movement/Approach Retreat")]
    public class ApproachRetreatMovementSO : EnemyMovementSO
    {
        [Tooltip("Chance this runs when the node starts. 1 = always. If the roll fails the node finishes instantly.")]
        [Range(0f, 1f)] [SerializeField] private float chance = 1f;
        [SerializeField] private float approachSpeed = 5f;
        [SerializeField] private float approachStopDistance = 0.5f;
        [SerializeField] private float retreatSpeed = 4f;
        [SerializeField] private float retreatDistance = 4f;
        [Min(0.5f)] [SerializeField] private float timeout = 6f;
        [SerializeField] private bool endOnApproachWhenCyclesSatisfied = true;
        [SerializeField] private MoveWindupSettings windup = new MoveWindupSettings();
        [SerializeField] private bool debugLog = false;

        public void Begin(EnemyContext ctx)
        {
            ApproachRetreatState st = ctx.ApproachRetreat;
            st.Elapsed = 0f;

            bool canRun = ctx.Target != null && (ctx.Health == null || ctx.Health.CurrentHealth > 0);
            bool rolled = chance >= 1f || (chance > 0f && Random.value < chance);

            if (!canRun || !rolled)
            {
                Log(ctx, $"skipped (canRun {canRun}, rolled {rolled})");
                st.Phase = ApproachPhase.Done;
                return;
            }

            Log(ctx, "started");
            st.Phase = ApproachPhase.Approach;
        }

        public bool IsFinished(EnemyContext ctx) => ctx.ApproachRetreat.Phase == ApproachPhase.Done;

        public override void Tick(EnemyContext ctx, float dt)
        {
            ApproachRetreatState st = ctx.ApproachRetreat;
            if (!st.Active) return;

            MoveWindup.BeginTick(ctx, windup);
            bool facingBefore = ctx.FacingRight;

            if (ctx.Target == null || ctx.IsAttacking)
            {
                st.Finish();
                ctx.Body.linearVelocity = new Vector2(0f, ctx.Body.linearVelocity.y);
                return;
            }

            st.Elapsed += dt;

            float selfX = ctx.Self.position.x;
            float dx = ctx.Target.position.x - selfX;
            int toTarget = dx >= 0f ? 1 : -1;
            bool timedOut = st.Elapsed >= timeout;
            float velocityX = 0f;

            ctx.FacingRight = toTarget > 0;

            if (st.Phase == ApproachPhase.Approach)
            {
                bool reached = Mathf.Abs(dx) <= approachStopDistance;
                bool blocked = !ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, toTarget);

                if (reached || blocked || timedOut)
                {
                    Log(ctx, $"approach done (reached {reached}, blocked {blocked}, timed out {timedOut})");

                    if (endOnApproachWhenCyclesSatisfied && ctx.ApproachCyclesSatisfied)
                    {
                        st.Phase = ApproachPhase.Done;
                    }
                    else
                    {
                        st.Phase = ApproachPhase.Retreat;
                        st.RetreatDir = -toTarget;
                        st.RetreatStartX = selfX;
                    }
                }
                else velocityX = toTarget * approachSpeed;
            }
            else
            {
                bool travelled = Mathf.Abs(selfX - st.RetreatStartX) >= retreatDistance;
                bool blocked = !ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, st.RetreatDir);

                if (travelled || blocked || timedOut)
                {
                    Log(ctx, $"retreat done (travelled {travelled}, blocked {blocked}, timed out {timedOut})");
                    ctx.ApproachCyclesSinceAttack++;
                    st.Phase = ApproachPhase.Done;
                }
                else velocityX = st.RetreatDir * retreatSpeed;
            }

            velocityX = MoveWindup.Process(ctx, windup, velocityX, facingBefore, dt);
            ctx.Body.linearVelocity = new Vector2(velocityX, ctx.Body.linearVelocity.y);
        }

        private void Log(EnemyContext ctx, string message)
        {
            if (debugLog) Debug.Log($"[ApproachRetreat] {ctx.Self.name}: {message}", ctx.Self);
        }
    }
}