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
        public float LockedTargetX;
        public bool SkipRetreat;
        public bool Interrupted;

        public bool Active => Phase == ApproachPhase.Approach || Phase == ApproachPhase.Retreat;

        public void Finish()
        {
            if (Active)
            {
                Phase = ApproachPhase.Done;
                Interrupted = true;
            }
        }

        public void Reset()
        {
            Phase = ApproachPhase.None;
            Interrupted = false;
        }
    }

    [CreateAssetMenu(menuName = "Enemies/Movement/Approach Retreat")]
    public class ApproachRetreatMovementSO : EnemyMovementSO
    {
        [SerializeField] private float approachSpeed = 5f;
        [SerializeField] private float approachStopDistance = 0.5f;
        [SerializeField] private float retreatSpeed = 4f;
        [SerializeField] private float retreatDistance = 4f;
        [Min(0.5f)] [SerializeField] private float timeout = 6f;
        [SerializeField] private bool lockTargetOnMoveStart = true;
        [SerializeField] private bool endOnApproachWhenCyclesSatisfied = true;
        [SerializeField] private MoveWindupSettings windup = new MoveWindupSettings();
        [SerializeField] private bool debugLog = false;

        public void Begin(EnemyContext ctx, bool skipRetreat = false)
        {
            ApproachRetreatState st = ctx.ApproachRetreat;
            st.Elapsed = 0f;
            st.SkipRetreat = skipRetreat;
            st.Interrupted = false;

            if (ctx.Target != null)
                st.LockedTargetX = ctx.Target.position.x;

            bool canRun = ctx.Target != null && (ctx.Health == null || ctx.Health.CurrentHealth > 0);

            if (!canRun)
            {
                Log(ctx, "skipped: not target or dead");
                st.Interrupted = true;
                st.Phase = ApproachPhase.Done;
                return;
            }

            Log(ctx, "started");
            st.Phase = ApproachPhase.Approach;
        }

        public bool IsFinished(EnemyContext ctx) => ctx.ApproachRetreat.Phase == ApproachPhase.Done;
        public bool WasInterrupted(EnemyContext ctx) => ctx.ApproachRetreat.Interrupted;

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

            MoveWindupState w = ctx.MoveWindup;

            if (!w.Moving && !w.WindingUp && !w.WindingDown)
                st.LockedTargetX = ctx.Target.position.x;

            float selfX = ctx.Self.position.x;
            float targetX = lockTargetOnMoveStart ? st.LockedTargetX : ctx.Target.position.x;
            float dx = targetX - selfX;
            int toTarget = dx >= 0f ? 1 : -1;
            bool timedOut = st.Elapsed >= timeout;
            float velocityX = 0f;

            ctx.FacingRight = toTarget > 0;

            if (st.Phase == ApproachPhase.Retreat)
                ctx.FacingRight = ctx.Target.position.x >= selfX;

            if (st.Phase == ApproachPhase.Approach)
            {
                bool reached = Mathf.Abs(dx) <= approachStopDistance;
                bool blocked = !ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, toTarget);

                if (reached || blocked || timedOut)
                {
                    Log(ctx, $"approach done (reached {reached}, blocked {blocked}, timed out {timedOut})");

                    if (st.SkipRetreat)
                    {
                        st.Phase = ApproachPhase.Done;
                    }
                    else
                    {
                        st.Phase = ApproachPhase.Retreat;
                        float liveDx = ctx.Target.position.x - selfX;
                        st.RetreatDir = liveDx >= 0f ? -1 : 1;
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