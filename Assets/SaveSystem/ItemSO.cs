using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Items/Item")]
public class ItemSO : ScriptableObject
{
    public string itemId;
    public string displayName;

    [Header("Damage Roll")]
    public int minDamage;
    public int maxDamage;

    [Header("Attack Roll")]
    public int minAttack;
    public int maxAttack;

    [Header("Defense Roll")]
    public int minDefense;
    public int maxDefense;

    [Header("Humanity Roll")]
    public int minHumanity;
    public int maxHumanity;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemId))
            itemId = name;
    }
}