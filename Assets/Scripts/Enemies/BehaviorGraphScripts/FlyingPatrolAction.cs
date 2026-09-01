using System;
using Enemies;
using Enemies.ModuleScripts.Movement;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "FlyingPatrolAction", story: "Fyling Agent patrols the area", category: "Action", id: "23a3e6913312bc824fd1dcbf56a3d919")]
public partial class FlyingPatrolAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<FlyingPatrolMovementSO> PatrolModule;

    private Enemy _enemy;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("FlyingPatrolAction: Agent is not linked on the Blackboard");
            return Status.Failure;
        }
        
        if (PatrolModule == null || PatrolModule.Value == null)
        {
            Debug.LogError("FlyingPatrolAction: PatrolModule SO is not assigned on the node");
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
        _enemy.RunMovement(PatrolModule.Value, Time.deltaTime);
        return Status.Running;
    }

    protected override void OnEnd()
    {
    }
}

