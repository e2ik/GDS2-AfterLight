using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLoreItemTemplateDB", menuName = "ScriptableObjects/LoreItemTemplateDB")]
public class LoreItemTemplateDB : ScriptableObject
{
    public List<LoreItemDefinition> loreItemTemplates;
}