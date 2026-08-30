using UnityEngine;

public enum SkillType
{
    Single,
    Timed
}

[CreateAssetMenu(fileName = "PrimaryGemBehaviourDefinition", menuName = "ScriptableObjects/PrimaryGemBehaviourDefinition")]
public abstract class PrimaryGemBehaviourDefinition : InventoryItemBase, IPrimaryGemBehaviour
{
    public string GemName;
    public string GemAttackDescription;

    [Header("Active Skill Vars")] 
    public SkillType SkillType;
    public float SkillRange;
    public float SkillDamageModifier = 1f;
    public abstract void Execute(AttackContext context, float baseDamage);
}