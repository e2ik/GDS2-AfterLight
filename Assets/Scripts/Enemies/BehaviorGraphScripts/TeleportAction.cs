using System;
using Enemies;
using Enemies.ModuleScripts.Movement;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Teleport Action", story: "Agent Teleports", category: "Action", id: "535f6af4818612b752aa8fb67b6c1901")]
public partial class TeleportAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<TeleportMovementSO> TeleportModule;

    private Enemy _enemy;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null)
        {
            Debug.LogError("TeleportAction: Agent is not linked on the Blackboard");
            return Status.Failure;
        }

        if (TeleportModule == null || TeleportModule.Value == null)
        {
            Debug.LogError("TeleportAction: TeleportModule SO is not assigned on the node");
            return Status.Failure;
        }

        if (!Agent.Value.TryGetComponent(out _enemy))
        {
            Debug.LogError("Agent has no Enemy component");
            return Status.Failure;
        }

        TeleportModule.Value.Begin(_enemy.Context);
        _enemy.MarkTeleportUsed();
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        _enemy.RunMovement(TeleportModule.Value, Time.deltaTime);
        return TeleportModule.Value.IsFinished(_enemy.Context) ? Status.Success : Status.Running;
    }
    

    protected override void OnEnd()
    {
        
    }
}

