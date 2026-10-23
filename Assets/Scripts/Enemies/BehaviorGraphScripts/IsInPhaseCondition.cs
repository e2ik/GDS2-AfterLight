using System;
using Enemies;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "IsInPhase", story: "[Agent] is in phase [Phase]", category: "Conditions", id: "e66a2374ad0573986667340cab44dfb4")]
public partial class IsInPhaseCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<int> Phase;

    private Enemy _enemy;

    public override bool IsTrue()
    {
        if (_enemy == null)
        {
            if (Agent == null || Agent.Value == null)
                return false;

            _enemy = Agent.Value.GetComponent<Enemy>();

            if (_enemy == null)
                return false;
        }
        
        return _enemy.Phase == Phase.Value;
    }
}
