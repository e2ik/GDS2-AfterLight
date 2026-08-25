using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SecondaryGemTemplateDB", menuName = "Scriptable Objects/SecondaryGemTemplateDB")]
public class SecondaryGemTemplateDB : ScriptableObject
{
    public List<SecondaryGemBehaviourDefinition> secondaryGemTemplates = new List<SecondaryGemBehaviourDefinition>();
}
