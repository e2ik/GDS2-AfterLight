using UnityEngine;

namespace Enemies.ModuleScripts
{
    [CreateAssetMenu(menuName = "Enemies/Movement/Chase")]
    public class ChaseMovementSO : EnemyMovementSO
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float facingDeadZone = 0.15f;

        [Header("Windup")]
        [SerializeField] private bool useWindup = false;
        [SerializeField, Min(0f)] private float windupDuration = 0.5f;
        [SerializeField] private string windupTrigger = "MoveWindup";
        [SerializeField, Min(0f)] private float winddownDuration = 0.5f;
        [SerializeField] private string winddownTrigger = "MoveWinddown";
        [SerializeField] private bool windupOnDirectionChange = true;
        [SerializeField] private bool holdAttacksUntilStopped = true;

        public override void Tick(EnemyContext ctx, float deltaTime)
        {
            if (useWindup && Time.frameCount - ctx.ChaseTickFrame > 2) ClearWindupState(ctx);
            ctx.ChaseTickFrame = Time.frameCount;

            if (ctx.IsAttacking)
            {
                ResetWindup(ctx);
                if (!ctx.IgnoreTerrainChecks)
                    EnemyTerrainProbe.StopAtEdge(ctx);
                return;
            }

            if (ctx.Target == null)
            {
                if (useWindup) TickWindup(ctx, false, ctx.ChaseMoveDir, deltaTime);
                ctx.Body.linearVelocity = new Vector2(0f, ctx.Body.linearVelocity.y);
                return;
            }

            float diff = ctx.Target.position.x - ctx.Self.position.x;
            bool arrived = Mathf.Abs(diff) <= facingDeadZone;

            int dir = arrived ? (ctx.FacingRight ? 1 : -1) : (diff >= 0f ? 1 : -1);

            bool holdFacing = useWindup && (ctx.ChaseWindingDown
                || (ctx.ChaseMoving && windupOnDirectionChange && winddownDuration > 0f && dir != ctx.ChaseMoveDir));
            if (!holdFacing)
                ctx.FacingRight = dir > 0;

            float moveX = ctx.TargetInRange || arrived ? 0f : dir * moveSpeed;

            if (moveX != 0f && !ctx.IgnoreTerrainChecks && EnemyTerrainProbe.IsBlocked(ctx, dir))
            {
                moveX = 0f;
                ctx.LastChaseBlockedTime = Time.time;
            }

            if (useWindup && !TickWindup(ctx, moveX != 0f, dir, deltaTime))
                moveX = 0f;

            ctx.Body.linearVelocity = new Vector2(moveX, ctx.Body.linearVelocity.y);
        }

        private bool TickWindup(EnemyContext ctx, bool wantsMove, int dir, float deltaTime)
        {
            bool canMove = AdvanceWindup(ctx, wantsMove, dir, deltaTime);
            ctx.ChaseHoldsAttacks = holdAttacksUntilStopped && (ctx.ChaseMoving || ctx.ChaseWindingDown);
            return canMove;
        }

        private bool AdvanceWindup(EnemyContext ctx, bool wantsMove, int dir, float deltaTime)
        {
            if (ctx.ChaseWindingDown)
            {
                ctx.ChaseWinddownTimer -= deltaTime;
                if (ctx.ChaseWinddownTimer > 0f) return false;

                ctx.ChaseWindingDown = false;
            }

            if (!wantsMove)
            {
                if (ctx.ChaseWindingUp)
                {
                    SetTrigger(ctx, windupTrigger, false);
                    ctx.ChaseWindingUp = false;
                }

                if (ctx.ChaseMoving) StartWinddown(ctx);
                return false;
            }

            if (ctx.ChaseMoving && windupOnDirectionChange && dir != ctx.ChaseMoveDir)
            {
                StartWinddown(ctx);
                if (ctx.ChaseWindingDown) return false;
            }

            if (!ctx.ChaseMoving)
            {
                if (!ctx.ChaseWindingUp)
                {
                    ctx.ChaseWindingUp = true;
                    ctx.ChaseWindupTimer = windupDuration;
                    SetTrigger(ctx, windupTrigger, true);
                }

                ctx.ChaseWindupTimer -= deltaTime;
                if (ctx.ChaseWindupTimer > 0f) return false;

                ctx.ChaseWindingUp = false;
                ctx.ChaseMoving = true;
            }

            ctx.ChaseMoveDir = dir;
            return true;
        }

        private void StartWinddown(EnemyContext ctx)
        {
            ctx.ChaseMoving = false;
            if (winddownDuration <= 0f) return;

            ctx.ChaseWindingDown = true;
            ctx.ChaseWinddownTimer = winddownDuration;
            SetTrigger(ctx, winddownTrigger, true);
        }

        private void ResetWindup(EnemyContext ctx)
        {
            if (!useWindup) return;

            if (ctx.ChaseWindingUp) SetTrigger(ctx, windupTrigger, false);
            if (ctx.ChaseWindingDown) SetTrigger(ctx, winddownTrigger, false);

            ClearWindupState(ctx);
        }

        private static void ClearWindupState(EnemyContext ctx)
        {
            ctx.ChaseWindingUp = false;
            ctx.ChaseWindingDown = false;
            ctx.ChaseMoving = false;
            ctx.ChaseWindupTimer = 0f;
            ctx.ChaseWinddownTimer = 0f;
            ctx.ChaseHoldsAttacks = false;
        }

        private static void SetTrigger(EnemyContext ctx, string trigger, bool set)
        {
            if (ctx.Animator == null || string.IsNullOrEmpty(trigger) || !HasTrigger(ctx.Animator, trigger)) return;

            if (set) ctx.Animator.SetTrigger(trigger);
            else ctx.Animator.ResetTrigger(trigger);
        }

        private static bool HasTrigger(Animator animator, string name)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name) return true;

            return false;
        }
    }
}