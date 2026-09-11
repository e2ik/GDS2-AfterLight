using UnityEngine;

[System.Serializable]
public struct RarityRange
{
    public Vector2 Common;
    public Vector2 Rare;
    public Vector2 Epic;
    public Vector2 Legendary;

    public Vector2 GetRange(ERarity rarity)
    {
        switch (rarity)
        {
            case ERarity.Common: return Common;
            case ERarity.Rare: return Rare;
            case ERarity.Epic: return Epic;
            case ERarity.Legendary: return Legendary;
            default: return Common;
        }
    }
}