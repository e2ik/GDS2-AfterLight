using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PrimaryGemTemplateDB", menuName = "Inventory/PrimaryGemTemplateDB")]
public class PrimaryGemTemplateDB : ScriptableObject
{
    public List<PrimaryGemBehaviourDefinition> primaryGemTemplates;
}