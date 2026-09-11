using UnityEngine;

public interface ISecondaryGemBehaviour
{
    void Modify(ref AttackContext context, SecondaryGemInstance instance);
    PassiveType Trigger(SecondaryGemInstance instance);
}