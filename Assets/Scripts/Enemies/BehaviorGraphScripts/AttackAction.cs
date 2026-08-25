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
    [SerializeReference] public BlackboardVariable<EnemyAttackSO> AttackModule;

    private Enemy _enemy;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("AttackAction: Agent is not linked on the Blackboard");
            return Status.Failure;
        }

        if (AttackModule == null || AttackModule.Value == null)
        {
            Debug.LogError("AttackAction: AttackModule SO is not assigned on the node");
            return Status.Failure;
        }

        if (!Agent.Value.TryGetComponent(out _enemy))
        {
            Debug.LogError("Agent has no Enemy component");
            return Status.Failure;
        }

        AttackModule.Value.Begin(_enemy.Context);
        
        return Status.Running;
    }

    protected override Status OnUpdate() =>
        AttackModule.Value.IsFinished(_enemy.Context) ? Status.Success : Status.Running;

    protected override void OnEnd()
    {
    }
}

