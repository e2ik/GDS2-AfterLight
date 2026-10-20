using FMODUnity;
using UnityEngine;

namespace Enemies
{
    public class EnemyMoveSfx : MonoBehaviour
    {
        [SerializeField] private Enemy enemy;
        [SerializeField] private EventReference windupEvent;
        [SerializeField] private EventReference moveLoopEvent;
        [SerializeField] private EventReference winddownEvent;
        [SerializeField] private bool stopWindupIfCancelled = true;

        [Header("Vulnerable Window")]
        [SerializeField] private AttackEvents attackEvents;
        [SerializeField] private EventReference vulnerableStartEvent;
        [SerializeField] private EventReference vulnerableLoopEvent;
        [SerializeField] private EventReference vulnerableEndEvent;

        private enum Phase { None, Windup, Moving, Winddown }
        private Phase phase;
        private FMOD.Studio.EventInstance windupInstance;
        private FMOD.Studio.EventInstance loopInstance;
        private FMOD.Studio.EventInstance winddownInstance;
        private FMOD.Studio.EventInstance vulnerableLoopInstance;
        private bool vulnerableOpen;
        private EnemyHealth health;

        private void Awake()
        {
            if (enemy == null) enemy = GetComponentInParent<Enemy>();
            if (enemy != null) health = enemy.GetComponentInChildren<EnemyHealth>(true);
            if (attackEvents == null && enemy != null) attackEvents = enemy.GetComponentInChildren<AttackEvents>(true);
        }

        private void OnEnable()
        {
            if (health != null) health.OnDeath += StopAll;
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath -= StopAll;
            StopAll();
        }

        private void LateUpdate()
        {
            UpdateVulnerable();
            UpdateMovePhase();
        }

        private void UpdateVulnerable()
        {
            bool open = attackEvents != null && attackEvents.BonusWindowOpen && IsAlive();
            if (open == vulnerableOpen) return;

            vulnerableOpen = open;
            GameObject target = enemy != null ? enemy.gameObject : gameObject;

            if (open)
            {
                if (!vulnerableStartEvent.IsNull) RuntimeManager.PlayOneShotAttached(vulnerableStartEvent, target);
                Play(vulnerableLoopEvent, ref vulnerableLoopInstance);
            }
            else
            {
                Stop(ref vulnerableLoopInstance);
                if (!vulnerableEndEvent.IsNull && IsAlive()) RuntimeManager.PlayOneShotAttached(vulnerableEndEvent, target);
            }
        }

        private bool IsAlive() => health == null || health.CurrentHealth > 0;

        private void UpdateMovePhase()
        {
            Phase next = GetPhase();
            if (next == phase) return;

            Phase previous = phase;
            phase = next;

            if (previous == Phase.Windup && next != Phase.Moving && stopWindupIfCancelled)
                Stop(ref windupInstance);

            if (previous == Phase.Moving)
                Stop(ref loopInstance);

            switch (next)
            {
                case Phase.Windup:
                    Stop(ref winddownInstance);
                    Play(windupEvent, ref windupInstance);
                    break;
                case Phase.Moving:
                    Play(moveLoopEvent, ref loopInstance);
                    break;
                case Phase.Winddown:
                    Play(winddownEvent, ref winddownInstance);
                    break;
            }
        }

        private Phase GetPhase()
        {
            if (enemy == null || !enemy.UsesMoveWindup || !enemy.isActiveAndEnabled) return Phase.None;
            if (!IsAlive()) return Phase.None;
            if (enemy.IsMoveWindingUp) return Phase.Windup;
            if (enemy.IsMoveMoving) return Phase.Moving;
            if (enemy.IsMoveWindingDown) return Phase.Winddown;
            return Phase.None;
        }

        private void Play(EventReference eventRef, ref FMOD.Studio.EventInstance instance)
        {
            Stop(ref instance);
            if (eventRef.IsNull) return;

            instance = RuntimeManager.CreateInstance(eventRef);
            RuntimeManager.AttachInstanceToGameObject(instance, enemy != null ? enemy.gameObject : gameObject);
            instance.start();
        }

        private void Stop(ref FMOD.Studio.EventInstance instance)
        {
            if (!instance.isValid()) return;

            instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            instance.release();
            instance.clearHandle();
        }

        private void StopAll()
        {
            Stop(ref windupInstance);
            Stop(ref loopInstance);
            Stop(ref winddownInstance);
            Stop(ref vulnerableLoopInstance);
            phase = Phase.None;
            vulnerableOpen = false;
        }
    }
}