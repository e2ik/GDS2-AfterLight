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
        public abstract void Begin(EnemyContext ctx);
        public abstract bool IsFinished(EnemyContext ctx);
    }
}
