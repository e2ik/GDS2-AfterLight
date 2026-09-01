using System;
using Enemies;
using Enemies.ModuleScripts.Movement;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "FlyingPosition", story: "Fyling Agent moves to Attack Position", category: "Action", id: "923c3f577fc8d06318d0753280003599")]
public partial class FlyingPositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<FlyingPositionMovementSO> PositionModule;

    private Enemy _enemy;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("FlyingPositionAction: Agent is not linked on the Blackboard");
            return Status.Failure;
        }
 
        if (PositionModule == null || PositionModule.Value == null)
        {
            Debug.LogError("FlyingPositionAction: PositionModule SO is not assigned on the node");
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
        _enemy.RunMovement(PositionModule.Value, Time.deltaTime);
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

