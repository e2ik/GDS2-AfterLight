using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GearTemplateDB", menuName = "Scriptable Objects/GearTemplateDB")]
public class GearTemplateDB : ScriptableObject
{
    public List<GearDefinition> gearTemplates = new List<GearDefinition>();
}
