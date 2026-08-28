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

        public float Range => range;
        public float CooldownDuration => cooldownDuration;
        
        public abstract void Begin(EnemyContext ctx);
        public abstract bool IsFinished(EnemyContext ctx);
    }
}
