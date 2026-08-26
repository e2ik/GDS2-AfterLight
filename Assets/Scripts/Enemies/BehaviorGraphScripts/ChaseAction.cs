using System;
using Enemies;
using Enemies.ModuleScripts;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Chase", story: "Agent Chases Target", category: "Action", id: "97652ff9fa1549a9d1a0a8f34124e5f9")]
public partial class ChaseAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<ChaseMovementSO> ChaseModule;

    private Enemy _enemy;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("ChaseAction: Agent is not linked on the Blackboard");
            return Status.Failure;
        }
        
        if (ChaseModule == null || ChaseModule.Value == null)
        {
            Debug.LogError("ChaseAction: ChaseModule SO is not assigned on the node");
            return Status.Failure;
        }

        if (!Agent.Value.TryGetComponent(out _enemy))
        {
            Debug.LogError("Agent has no Enemy component");
            return Status.Failure;
        }
        
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        _enemy.RunMovement(ChaseModule.Value, Time.deltaTime);

        return Status.Running;
    }

    protected override void OnEnd()
    {
    }
}

