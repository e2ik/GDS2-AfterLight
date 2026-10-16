using System;
using Enemies;
using Enemies.ModuleScripts.Movement;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Unity.VisualScripting;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Approach Retreat", story: "Agent approaches then retreats", category: "Action", id: "c3d7a1e85f2b49a08b6e4d1f7a9c2e53")]
public partial class ApproachRetreatAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<ApproachRetreatMovementSO> Module;
    [SerializeReference] public BlackboardVariable<int> Cycles;
    [SerializeReference] public BlackboardVariable<bool> EndWithApproach;

    private Enemy _enemy;
    private int _cyclesDone;
    private bool _finalApproachStarted;

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

        _cyclesDone = 0;
        _finalApproachStarted = false;
        
        return BeginNextPass() ? Status.Running : Status.Success;
    }

    protected override Status OnUpdate()
    {
        _enemy.RunMovement(Module.Value, Time.deltaTime);

        EnemyContext ctx = _enemy.Context;

        if (!Module.Value.IsFinished(ctx))
            return Status.Running;

        if (Module.Value.WasInterrupted(ctx))
            return Status.Failure;

        if (!_finalApproachStarted)
            _cyclesDone++;

        return BeginNextPass() ? Status.Running : Status.Success;
    }

    protected override void OnEnd()
    {
        _enemy?.Context.ApproachRetreat.Reset();
    }

    private bool BeginNextPass()
    {
        EnemyContext ctx = _enemy.Context;
        int cycles = Cycles != null ? Mathf.Max(0, Cycles.Value) : 1;
        bool endWithApproach = EndWithApproach != null && EndWithApproach.Value;
        int fullCycles = endWithApproach ? Mathf.Max(0, cycles - 1) : cycles;

        if (_cyclesDone < fullCycles)
        {
            Module.Value.Begin(ctx, skipRetreat: false);
            return true;
        }

        if (endWithApproach && cycles > 0 && !_finalApproachStarted)
        {
            _finalApproachStarted = true;
            Module.Value.Begin(ctx, skipRetreat: true);
            return true;
        }

        return false;
    }
}
