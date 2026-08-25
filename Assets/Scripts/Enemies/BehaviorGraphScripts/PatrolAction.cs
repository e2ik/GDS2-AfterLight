using System;
using Enemies;
using Enemies.ModuleScripts;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Patrol", story: "Temp Enemy Patrols", category: "Action", id: "e4cf82e666ae983b4751367e1bb4f6be")]
public partial class PatrolAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<PatrolMovementSO> PatrolModule;

    private Enemy _enemy;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null) 
        {
            Debug.LogError("PatrolAction: Agent is not linked on the Blackboard.");
            return Status.Failure;
        }
        
        if (PatrolModule == null || PatrolModule.Value == null)
        { 
            Debug.LogError("PatrolAction: PatrolModule SO is not assigned on the node.");
            return Status.Failure;
        }

        if (!Agent.Value.TryGetComponent(out _enemy))
        {
            Debug.LogError("Agent has no Enemy Component");
            return Status.Failure;
        }
        
        return Status.Running;
    }

    protected override Status OnUpdate()
    {   
        _enemy.RunMovement(PatrolModule.Value, Time.deltaTime);
        return _enemy.Context.TargetVisible ? Status.Success : Status.Running;
    }

    protected override void OnEnd()
    {
    }
}

