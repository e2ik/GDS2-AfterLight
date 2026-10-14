using System;
using Enemies;
using Enemies.ModuleScripts.Movement;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Approach Retreat", story: "Agent approaches then retreats", category: "Action", id: "c3d7a1e85f2b49a08b6e4d1f7a9c2e53")]
public partial class ApproachRetreatAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<ApproachRetreatMovementSO> Module;

    private Enemy _enemy;

    protected override Status OnStart()
    {
        if (Agent == null || Agent.Value == null || Module == null || Module.Value == null)
        {
            Debug.LogError("ApproachRetreatAction: Agent or Module is not assigned");
            return Status.Failure;
        }

        if (!Agent.Value.TryGetComponent(out _enemy))
        {
            Debug.LogError("Agent has no Enemy component");
            return Status.Failure;
        }

        Module.Value.Begin(_enemy.Context);
        return Module.Value.IsFinished(_enemy.Context) ? Status.Success : Status.Running;
    }

    protected override Status OnUpdate()
    {
        _enemy.RunMovement(Module.Value, Time.deltaTime);
        return Module.Value.IsFinished(_enemy.Context) ? Status.Success : Status.Running;
    }

    protected override void OnEnd()
    {
        _enemy?.Context.ApproachRetreat.Reset();
    }
}
