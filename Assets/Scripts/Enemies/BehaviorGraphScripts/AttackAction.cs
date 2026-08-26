using System;
using Enemies;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "AttackAction", story: "Agent Attacks Target", category: "Action", id: "ce5ad34b6172ace31ac02efd87bca037")]
public partial class AttackAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;

    private Enemy _enemy;
    private AttackInstance _selected;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("AttackAction: Agent is not linked on the Blackboard");
            return Status.Failure;
        }

        if (!Agent.Value.TryGetComponent(out _enemy))
        {
            Debug.LogError("Agent has no Enemy component");
            return Status.Failure;
        }
        
        if (!_enemy.TrySelectAttack(out _selected))
        {
            Debug.LogWarning("AttackAction: no valid attack available.");
            return Status.Failure;
        }
        
        _selected.Begin(_enemy.Context);
        
        return Status.Running;
    }

    protected override Status OnUpdate() =>
        _selected.IsFinished(_enemy.Context) ? Status.Success : Status.Running;

    protected override void OnEnd()
    {
    }
}

