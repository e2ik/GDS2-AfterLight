using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "RollMixedAttack", story: "Roll wether the next attack is a projectile", category: "Action", id: "5c980185c8643811809a2640f5aab188")]
public partial class RollMixedAttackAction : Action
{
    
    [SerializeReference] public BlackboardVariable<float> ProjectileChance;
    [SerializeReference] public BlackboardVariable<int> GuaranteeMeleeAfter;
    [SerializeReference] public BlackboardVariable<bool> ProjectilePlanned;

    private int _projectileStreak;

    protected override Status OnStart()
    {
        int pityAfter = GuaranteeMeleeAfter != null ? GuaranteeMeleeAfter.Value : 0;
        bool forceMelee = pityAfter > 0 && _projectileStreak >= pityAfter;
        float chance = ProjectileChance != null ? Mathf.Clamp01(ProjectileChance.Value) : 0f;

        bool projectile = !forceMelee && UnityEngine.Random.value < chance;
        _projectileStreak = projectile ? _projectileStreak + 1 : 0;

        ProjectilePlanned.Value = projectile;
        Debug.Log($"MixedAttack roll: chance {chance}, projectile {projectile}, forceMelee {forceMelee}, streak {_projectileStreak}");
        return Status.Success;
    }
}

