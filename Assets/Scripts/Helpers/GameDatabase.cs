using UnityEngine;

public class GameDatabase : MonoBehaviour
{
    [SerializeField]
    private SecondaryGemTemplateDB secondaryGemTemplateDB;
    [SerializeField] private GearTemplateDB gearTemplateDB;
    [SerializeField] private PrimaryGemTemplateDB primaryGemTemplateDB;

    public static SecondaryGemTemplateDB SecondaryGemTemplateDB{get; private set;}
    public static GearTemplateDB GearTemplateDB{get; private set;}
    public static PrimaryGemTemplateDB PrimaryGemTemplateDB{get; private set;}

    void Awake()
    {
        SecondaryGemTemplateDB = secondaryGemTemplateDB;
        GearTemplateDB = gearTemplateDB;
        PrimaryGemTemplateDB = primaryGemTemplateDB;
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

    public static PrimaryGemBehaviourDefinition GetPrimaryTemplateFromID(string itemID)
    {
        foreach(var template in PrimaryGemTemplateDB.primaryGemTemplates)
        {
            if(template.ItemID == itemID)
            {
                return template;
            }
        }
        Debug.Log($"ItemID {itemID} not found. ");
        return null;
    }
}
