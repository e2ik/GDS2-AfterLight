using UnityEngine;

public interface ISecondaryGemBehaviour
{
    void Modify(ref AttackContext context, SecondaryGemInstance instance);
    PassiveType GetPassiveType(SecondaryGemInstance instance);
}