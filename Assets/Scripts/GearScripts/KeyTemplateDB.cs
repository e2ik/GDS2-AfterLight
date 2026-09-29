using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewKeyTemplateDB", menuName = "ScriptableObjects/KeyTemplateDB")]
public class KeyTemplateDB : ScriptableObject
{
    public List<KeyDefinition> keyTemplates;
}