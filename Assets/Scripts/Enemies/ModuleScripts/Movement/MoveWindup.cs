using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enemies.ModuleScripts.Movement
{
    [Serializable]
    public class MoveWindupSettings
    {
        public bool enabled = false;
        public string windupTrigger = "MoveWindup";
        public string windupState = "MoveWindup";
        public string winddownTrigger = "MoveWinddown";
        public string winddownState = "MoveWinddown";
        [Min(0.1f)] public float phaseTimeout = 3f;
        [Min(0f)] public float startDelay = 0.15f;
        public bool windupOnDirectionChange = true;
        public bool holdAttacksUntilStopped = true;
    }

    public class MoveWindupState
    {
        public MoveWindupSettings Settings;
        public bool Moving, WindingUp, WindingDown;
        public bool WindupEntered, WinddownEntered;
        public float WindupElapsed, WinddownElapsed, WantTimer;
        public int Dir;
        public int LastTickFrame = -10;
        public HashSet<string> TriggerNames;

        private bool Fresh => Time.frameCount - LastTickFrame <= 2;

        public bool InPhase =>
            Settings != null && Settings.enabled && Fresh && (WindingUp || Moving || WindingDown);

        public bool HoldsAttacks =>
            Settings != null && Settings.enabled && Settings.holdAttacksUntilStopped && Fresh && (Moving || WindingDown);
    }

    public static class MoveWindup
    {
        public static void BeginTick(EnemyContext ctx, MoveWindupSettings s)
        {
            if (s == null || !s.enabled) return;

            MoveWindupState st = ctx.MoveWindup;
            if (Time.frameCount - st.LastTickFrame > 2) Clear(st);
            st.LastTickFrame = Time.frameCount;
            st.Settings = s;
        }
        
        public static float Process(EnemyContext ctx, MoveWindupSettings s, float velocityX, bool facingBefore, float dt)
        {
            if (s == null || !s.enabled) return velocityX;

            MoveWindupState st = ctx.MoveWindup;
            bool wantsMove = Mathf.Abs(velocityX) > 0.01f;
            int dir = wantsMove ? (velocityX > 0f ? 1 : -1) : st.Dir;

            if (Advance(ctx, st, s, wantsMove, dir, dt)) return velocityX;

            if (st.WindingDown) ctx.FacingRight = facingBefore;
            return 0f;
        }

        public static void Reset(EnemyContext ctx)
        {
            MoveWindupState st = ctx.MoveWindup;
            MoveWindupSettings s = st.Settings;

            if (s != null)
            {
                if (st.WindingUp) SetTrigger(ctx, st, s.windupTrigger, false);
                if (st.WindingDown) SetTrigger(ctx, st, s.winddownTrigger, false);
            }

            Clear(st);
        }

        private static void Clear(MoveWindupState st)
        {
            st.Moving = st.WindingUp = st.WindingDown = false;
            st.WindupEntered = st.WinddownEntered = false;
            st.WindupElapsed = st.WinddownElapsed = st.WantTimer = 0f;
        }

        private static bool Advance(EnemyContext ctx, MoveWindupState st, MoveWindupSettings s, bool wantsMove, int dir, float dt)
        {
            if (st.WindingDown)
            {
                st.WinddownElapsed += dt;
                if (!PhaseFinished(ctx, s, s.winddownState, ref st.WinddownEntered, st.WinddownElapsed)) return false;

                st.WindingDown = false;
            }

            if (!wantsMove)
            {
                st.WantTimer = 0f;

                if (st.WindingUp)
                {
                    SetTrigger(ctx, st, s.windupTrigger, false);
                    st.WindingUp = false;
                }

                if (st.Moving) StartWinddown(ctx, st, s);
                return false;
            }

            if (st.Moving && s.windupOnDirectionChange && dir != st.Dir)
            {
                StartWinddown(ctx, st, s);
                if (st.WindingDown) return false;
            }

            if (!st.Moving)
            {
                if (!st.WindingUp)
                {
                    st.WantTimer += dt;
                    if (st.WantTimer < s.startDelay) return false;
                    st.WantTimer = 0f;

                    if (!HasTrigger(ctx, st, s.windupTrigger))
                    {
                        st.Moving = true;
                        st.Dir = dir;
                        return true;
                    }

                    st.WindingUp = true;
                    st.WindupEntered = false;
                    st.WindupElapsed = 0f;
                    SetTrigger(ctx, st, s.windupTrigger, true);
                }

                st.WindupElapsed += dt;
                if (!PhaseFinished(ctx, s, s.windupState, ref st.WindupEntered, st.WindupElapsed)) return false;

                st.WindingUp = false;
                st.Moving = true;
            }

            st.Dir = dir;
            return true;
        }

        private static void StartWinddown(EnemyContext ctx, MoveWindupState st, MoveWindupSettings s)
        {
            st.Moving = false;
            if (!HasTrigger(ctx, st, s.winddownTrigger)) return;

            st.WindingDown = true;
            st.WinddownEntered = false;
            st.WinddownElapsed = 0f;
            SetTrigger(ctx, st, s.winddownTrigger, true);
        }

        private static bool PhaseFinished(EnemyContext ctx, MoveWindupSettings s, string stateName, ref bool entered, float elapsed)
        {
            if (elapsed >= s.phaseTimeout) return true;

            Animator animator = ctx.Animator;
            if (animator == null || string.IsNullOrEmpty(stateName)) return true;

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(stateName))
            {
                entered = true;
                return info.normalizedTime >= 1f;
            }

            return entered;
        }

        private static bool HasTrigger(EnemyContext ctx, MoveWindupState st, string trigger)
        {
            Animator animator = ctx.Animator;
            if (animator == null || string.IsNullOrEmpty(trigger)) return false;

            if (st.TriggerNames == null)
            {
                st.TriggerNames = new HashSet<string>();
                foreach (AnimatorControllerParameter p in animator.parameters)
                    if (p.type == AnimatorControllerParameterType.Trigger) st.TriggerNames.Add(p.name);
            }

            return st.TriggerNames.Contains(trigger);
        }

        private static void SetTrigger(EnemyContext ctx, MoveWindupState st, string trigger, bool set)
        {
            if (!HasTrigger(ctx, st, trigger)) return;

            if (set) ctx.Animator.SetTrigger(trigger);
            else ctx.Animator.ResetTrigger(trigger);
        }
    }
}