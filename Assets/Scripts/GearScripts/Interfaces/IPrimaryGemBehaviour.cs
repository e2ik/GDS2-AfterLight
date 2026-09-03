using UnityEngine;



public interface IPrimaryGemBehaviour
{
    void Execute(AttackContext context, float baseDamage, float chargeAmount = 0f);
}
