using UnityEngine;

public class GameDatabase : MonoBehaviour
{
    [SerializeField]
    private SecondaryGemTemplateDB secondaryGemTemplateDB;
    [SerializeField] private GearTemplateDB gearTemplateDB;

    public static SecondaryGemTemplateDB SecondaryGemTemplateDB{get; private set;}
    public static GearTemplateDB GearTemplateDB{get; private set;}
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SecondaryGemTemplateDB = secondaryGemTemplateDB;
        GearTemplateDB = gearTemplateDB;
    }

    public static SecondaryGemBehaviourDefinition GetSecondaryTemplateFromID(string templateID)
    {
        foreach(var template in SecondaryGemTemplateDB.secondaryGemTemplates)
        {
            if(template.TemplateID == templateID)
            {
                return template;
            }
        }
        Debug.Log($"TemplateID {templateID} not found. ");
        return null;
    }

    public static GearDefinition GetGearTemplateFromID(string templateID)
    {
        foreach(var template in GearTemplateDB.gearTemplates)
        {
            if(template.TemplateID == templateID)
            {
                return template;
            }
        }
        Debug.Log($"TemplateID {templateID} not found. ");
        return null;
    }
}
