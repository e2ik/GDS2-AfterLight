using System;
using Enemies;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "HealthBelowPercentCondition", story: "[Agent] health is below [Percent]", category: "Conditions", id: "5c8dd100c834c9a0e664f7b707f34cee")]
public partial class HealthBelowPercentCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<float> Percent;

    private EnemyHealth _health;

    public override bool IsTrue()
    {
        if (_health == null)
        {
            if (Agent == null || Agent.Value == null)
                return false;
            
            _health = Agent.Value.GetComponent<EnemyHealth>();

            if (_health == null)
                return false;
        }

        return _health.CurrentHealth <= _health.MaxHealth * Percent.Value;
    }
}
