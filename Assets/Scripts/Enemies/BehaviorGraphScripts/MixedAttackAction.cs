using System;
using Enemies;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "MixedAttackAction", story: "Agent attacks with melee or projectile", category: "Action", id: "152c2809d50bb3d3fe5ad704e39aef91")]
public partial class MixedAttackAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    
    [Tooltip("Leave empty for melee only.")]
    [SerializeReference] public BlackboardVariable<EnemyAttackSO> ProjectileAttack;
    [SerializeReference] public BlackboardVariable<float> ProjectileChance; 
    [SerializeReference] public BlackboardVariable<int> GuaranteeMeleeAfter; 
    [SerializeReference] public BlackboardVariable<float> WaitTimeout; 
    
    private Enemy _enemy;
    private AttackInstance _running;
    private bool _preferProjectile;
    private bool _forceMelee;
    private bool _hasProjectile;
    private float _waited;
    private int _projectileStreak;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("MixedAttackAction: Agent is not linked on the Blackboard");
            return Status.Failure;
        }

        if (!Agent.Value.TryGetComponent(out _enemy))
        {
            Debug.LogError("Agent has no Enemy component");
            return Status.Failure;
        }

        _running = null;
        _waited = 0f;
        _hasProjectile = ProjectileAttack != null && ProjectileAttack.Value != null;

        int pityAfter = GuaranteeMeleeAfter != null ? GuaranteeMeleeAfter.Value : 0;
        _forceMelee = pityAfter > 0 && _projectileStreak >= pityAfter;
        float chance = ProjectileChance != null ? Mathf.Clamp01(ProjectileChance.Value) : 0f;
        _preferProjectile = _hasProjectile && !_forceMelee && UnityEngine.Random.value < chance;
        Debug.Log($"MixedAttack roll: chance {chance}, projectileStreak {_projectileStreak}, preferProjectile {_preferProjectile}, forceMelee {_forceMelee}");

        TryBegin();
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (_running == null)
        {
            if (TryBegin()) 
                return Status.Running;

            _waited += Time.deltaTime;
            float timeout = WaitTimeout != null ? WaitTimeout.Value : 0f;
            
            return _waited >= timeout ? Status.Failure : Status.Running;
        }

        return _running.IsFinished(_enemy.Context) ? Status.Success : Status.Running;
    }

    protected override void OnEnd()
    {
        if (_running != null)
        {
            _enemy?.MarkAttackEnded();
            _running = null;
        }
    }
    
    private bool TryBegin()
    {
        EnemyContext ctx = _enemy.Context;
        if (_enemy.IsAttacking || ctx.ApproachRetreat.Active || ctx.MoveWindup.HoldsAttacks) 
            return false;

        AttackInstance projectile = null;
        bool projectileValid = _hasProjectile
                               && _enemy.TryGetAttackInstance(ProjectileAttack.Value, out projectile)
                               && projectile.IsValid;

        bool meleeValid = _enemy.TrySelectAttack(out AttackInstance melee);

        bool useProjectile;

        if (_preferProjectile)
        {
            if (!projectileValid)
                return false;
            useProjectile = true;
        }
        else if (_forceMelee)
            useProjectile = false;
        else
            useProjectile = !meleeValid && projectileValid;

        AttackInstance chosen = useProjectile ? projectile : (meleeValid ? melee : null);
        if (chosen == null) 
            return false;

        _enemy.MarkAttackStarted();
        chosen.Begin(ctx);
        _running = chosen;

        _projectileStreak = useProjectile ? _projectileStreak + 1 : 0;
        
        return true;
    }
}

