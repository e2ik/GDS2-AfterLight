using System.Collections.Generic;
using UnityEngine;

public class GameDatabase : MonoBehaviour
{
    [SerializeField] private SecondaryGemTemplateDB secondaryGemTemplateDB;
    [SerializeField] private GearTemplateDB gearTemplateDB;
    [SerializeField] private PrimaryGemTemplateDB primaryGemTemplateDB;
    [SerializeField] private WeaponTemplateDB weaponTemplateDB;
    [SerializeField] private KeyTemplateDB keyTemplateDB;
    [SerializeField] private LoreItemTemplateDB loreItemTemplateDB;

    public static SecondaryGemTemplateDB SecondaryGemTemplateDB{get; private set;}
    public static GearTemplateDB GearTemplateDB{get; private set;}
    public static PrimaryGemTemplateDB PrimaryGemTemplateDB{get; private set;}
    public static WeaponTemplateDB WeaponTemplateDB{get; private set;}
    public static KeyTemplateDB KeyTemplateDB{get; private set;}
    public static LoreItemTemplateDB LoreItemTemplateDB{get; private set;}

    // ID -> template lookups, built once in Awake so each Get...FromID call is a
    // dictionary hit instead of a scan through the whole list.
    private static Dictionary<string, SecondaryGemBehaviourDefinition> secondaryByID;
    private static Dictionary<string, GearDefinition> gearByID;
    private static Dictionary<string, PrimaryGemBehaviourDefinition> primaryByID;
    private static Dictionary<string, WeaponDefinition> weaponByID;
    private static Dictionary<string, KeyDefinition> keyByID;
    private static Dictionary<string, LoreItemDefinition> loreByID;

    void Awake()
    {
        SecondaryGemTemplateDB = secondaryGemTemplateDB;
        GearTemplateDB = gearTemplateDB;
        PrimaryGemTemplateDB = primaryGemTemplateDB;
        WeaponTemplateDB = weaponTemplateDB;
        KeyTemplateDB = keyTemplateDB;
        LoreItemTemplateDB = loreItemTemplateDB;

        secondaryByID = BuildLookup(SecondaryGemTemplateDB?.secondaryGemTemplates);
        gearByID = BuildLookup(GearTemplateDB?.gearTemplates);
        primaryByID = BuildLookup(PrimaryGemTemplateDB?.primaryGemTemplates);
        weaponByID = BuildLookup(WeaponTemplateDB?.weaponTemplates);
        keyByID = BuildLookup(KeyTemplateDB?.keyTemplates);
        loreByID = BuildLookup(LoreItemTemplateDB?.loreItemTemplates);
    }

    private static Dictionary<string, T> BuildLookup<T>(IEnumerable<T> templates) where T : class
    {
        var lookup = new Dictionary<string, T>();

        if (templates == null)
        {
            Debug.LogWarning($"[GameDatabase] {typeof(T).Name} template list is null — is the DB asset assigned on GameDatabase?");
            return lookup;
        }

        foreach (var template in templates)
        {
            if (template == null) continue;

            string id = GetTemplateID(template);
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[GameDatabase] {typeof(T).Name} \"{template}\" has no ID set.");
                continue;
            }

            if (lookup.TryGetValue(id, out T existing))
            {
                Debug.LogWarning($"[GameDatabase] Duplicate {typeof(T).Name} ID \"{id}\": \"{existing}\" and \"{template}\". " +
                                  "Keeping the first one. Give each asset a unique ID.");
                continue;
            }

            lookup.Add(id, template);
        }

        return lookup;
    }

    private static T Lookup<T>(Dictionary<string, T> lookup, string id) where T : class
    {
        if (lookup == null || string.IsNullOrEmpty(id)) return null;
        if (lookup.TryGetValue(id, out T template)) return template;

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

    public static SecondaryGemBehaviourDefinition GetSecondaryTemplateFromID(string id) => Lookup(secondaryByID, id);

    public static GearDefinition GetGearTemplateFromID(string id) => Lookup(gearByID, id);

    public static PrimaryGemBehaviourDefinition GetPrimaryTemplateFromID(string id) => Lookup(primaryByID, id);

    public static WeaponDefinition GetWeaponTemplateFromID(string id) => Lookup(weaponByID, id);

    public static KeyDefinition GetKeyTemplateFromID(string id) => Lookup(keyByID, id);

    public static LoreItemDefinition GetLoreItemTemplateFromID(string id) => Lookup(loreByID, id);
}