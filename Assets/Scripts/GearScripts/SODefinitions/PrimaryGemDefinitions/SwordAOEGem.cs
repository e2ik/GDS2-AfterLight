using UnityEngine;

[CreateAssetMenu(fileName = "Spin Attack Gem", menuName = "Primary Gems/Spin Attack Gem")]
public class SwordAOEGem : PrimaryGemBehaviourDefinition
{
    public override void Execute(AttackContext context)
    {
        Debug.Log("Spin To Win");
    }
}
