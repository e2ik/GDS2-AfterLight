using UnityEngine;

public enum SkillType
{
    Single,
    Timed
}

public enum SkillExecutionType
{
    Charged,
    Held
}

[CreateAssetMenu(fileName = "PrimaryGemBehaviourDefinition", menuName = "ScriptableObjects/PrimaryGemBehaviourDefinition")]
public abstract class PrimaryGemBehaviourDefinition : InventoryItemBase, IPrimaryGemBehaviour
{
    public string GemName;
    public string GemAttackDescription;

    [Header("Active Skill Vars")] 
    public SkillType SkillType;
    public SkillExecutionType SkillExecutionType;
    public float SkillRange;
    public float SkillDamageModifier = 1f;
    public abstract void Execute(AttackContext context, float baseDamage);
    public float MinimumHeldDuration = 0.25f; // so hold skills don't cancel immediately
}