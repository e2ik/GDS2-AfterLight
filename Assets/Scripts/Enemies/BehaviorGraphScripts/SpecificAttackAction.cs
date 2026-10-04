using System;
using Enemies;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "SpecificAttack", story: "Agent uses Specific Attack", category: "Action", id: "4fcf735472d7b63d68a8e92f5b60221b")]
public partial class SpecificAttackAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<EnemyAttackSO> SpecificAttack;

    private Enemy _enemy;
    private AttackInstance _instance;
    

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("SpecificAttackAction: Agent is not linked on the Blackboard");
            return Status.Failure;
        }

        if (SpecificAttack == null || SpecificAttack.Value == null)
        {
            Debug.LogError("SpecificAttackAction: SpecificAttack SO is not assigned on the node");
            return Status.Failure;
        }

        if (!Agent.Value.TryGetComponent(out _enemy))
        {
            Debug.LogError("Agent has no Enemy component");
            return Status.Failure;
        }

        if (!_enemy.TryGetAttackInstance(SpecificAttack.Value, out _instance))
        {
            Debug.LogError($"SpecificAttackAction: {SpecificAttack.Value.name} is not in {_enemy.name}'s attacks list");
            return Status.Failure;
        }

        if (!_instance.IsValid)
            return Status.Failure;

        _enemy.MarkAttackStarted();
        _instance.Begin(_enemy.Context);

        return Status.Running;
    }

    protected override Status OnUpdate() => _instance.IsFinished(_enemy.Context) ? Status.Success : Status.Running;

    protected override void OnEnd()
    {
        if (_instance != null)
        {
            _enemy?.MarkAttackEnded();
            _instance = null;
        }
    }
}

