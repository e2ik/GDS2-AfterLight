using System.Collections.Generic;
using UnityEngine;

public class GameDatabase : MonoBehaviour
{
    [SerializeField] private SecondaryGemTemplateDB secondaryGemTemplateDB;
    [SerializeField] private GearTemplateDB gearTemplateDB;
    [SerializeField] private PrimaryGemTemplateDB primaryGemTemplateDB;
    [SerializeField] private WeaponTemplateDB weaponTemplateDB;

    public static SecondaryGemTemplateDB SecondaryGemTemplateDB{get; private set;}
    public static GearTemplateDB GearTemplateDB{get; private set;}
    public static PrimaryGemTemplateDB PrimaryGemTemplateDB{get; private set;}
    public static WeaponTemplateDB WeaponTemplateDB{get; private set;}

    void Awake()
    {
        SecondaryGemTemplateDB = secondaryGemTemplateDB;
        GearTemplateDB = gearTemplateDB;
        PrimaryGemTemplateDB = primaryGemTemplateDB;
        WeaponTemplateDB = weaponTemplateDB;
    }

    private static T FindByID<T>(IEnumerable<T> templates, string id) where T : class
    {
        if (templates == null)
        {
            Debug.LogWarning("[GameDatabase] Template list is null — is the DB asset assigned on GameDatabase?");
            return null;
        }

        foreach (var template in templates)
        {
            if (GetTemplateID(template) == id) return template;
        }

        Debug.Log($"ID {id} not found.");
        return null;
    }

    private static string GetTemplateID<T>(T template) where T : class
    {
        return template switch
        {
            SecondaryGemBehaviourDefinition s => s.TemplateID,
            GearDefinition g => g.TemplateID,
            InventoryItemBase i => i.ItemID,
            _ => null
        };
    }

    public static SecondaryGemBehaviourDefinition GetSecondaryTemplateFromID(string templateID) =>
        FindByID(SecondaryGemTemplateDB?.secondaryGemTemplates, templateID);

    public static GearDefinition GetGearTemplateFromID(string templateID) =>
        FindByID(GearTemplateDB?.gearTemplates, templateID);

    public static PrimaryGemBehaviourDefinition GetPrimaryTemplateFromID(string itemID) =>
        FindByID(PrimaryGemTemplateDB?.primaryGemTemplates, itemID);

    public static WeaponDefinition GetWeaponTemplateFromID(string itemID) =>
        FindByID(WeaponTemplateDB?.weaponTemplates, itemID);
}