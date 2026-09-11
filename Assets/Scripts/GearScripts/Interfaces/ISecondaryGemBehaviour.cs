using UnityEngine;

public interface ISecondaryGemBehaviour
{
    void Modify(ref AttackContext context, SecondaryGemInstance instance);
    void Trigger(SecondaryGemInstance instance);
}