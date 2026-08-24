using UnityEngine;

[CreateAssetMenu(fileName = "PrimaryGemBehaviourDefinition", menuName = "ScriptableObjects/PrimaryGemBehaviourDefinition")]
public abstract class PrimaryGemBehaviourDefinition : ScriptableObject, IPrimaryGemBehaviour
{
    public string GemName;
    public string GemAttackDescription;
    public abstract void Execute(AttackContext context);
}