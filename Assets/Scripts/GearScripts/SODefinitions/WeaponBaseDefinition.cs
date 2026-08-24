using UnityEngine;

[CreateAssetMenu(fileName = "WeaponDefinition", menuName = "ScriptableObjects/WeaponDefinition")]
public class WeaponDefinition : ScriptableObject
{
    public float BaseWeaponDamage;
    public float BaseWeaponRange;
    public float BaseWeaponCrit;

    public PrimaryGemBehaviourDefinition PrimaryGemBehaviour;
    public SecondaryGemBehaviourDefinition SecondaryGemBehaviour;
}