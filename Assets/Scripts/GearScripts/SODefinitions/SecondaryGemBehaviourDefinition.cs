using UnityEngine;


[CreateAssetMenu(fileName = "SecondaryGemBehaviourDefintion", menuName = "ScriptableObjects/SecondaryGemBehaviourDefinition")]

public abstract class SecondaryGemBehaviourDefinition : ScriptableObject, ISecondaryGemBehaviour
{
    public abstract void Modify(ref AttackContext context);
}