using UnityEngine;


[CreateAssetMenu(fileName = "SecondaryGemBehaviourDefintion", menuName = "ScriptableObjects/SecondaryGemBehaviourDefinition")]

public abstract class SecondaryGemBehaviourDefinition : InventoryItemBase, ISecondaryGemBehaviour
{
    public abstract void Modify(ref AttackContext context);
}