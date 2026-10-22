using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LootRarityZone : MonoBehaviour
{
    [SerializeField] private RarityWeights odds = new RarityWeights { common = 60f, rare = 35f, epic = 5f, legendary = 0f };
    [SerializeField, Range(0f, 1f)] private float worseThanOwnedNoDropChance = 0.8f;
    [SerializeField] private bool debugRolls = false;

    private static readonly Dictionary<int, LootRarityZone> zones = new Dictionary<int, LootRarityZone>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetZones() => zones.Clear();

    private void OnEnable()
    {
        zones[gameObject.scene.handle] = this;
    }

    private void OnDisable()
    {
        if (zones.TryGetValue(gameObject.scene.handle, out LootRarityZone zone) && zone == this)
            zones.Remove(gameObject.scene.handle);
    }

    public static bool TryGet(Scene scene, out LootRarityZone zone)
    {
        zone = null;
        return scene.IsValid() && zones.TryGetValue(scene.handle, out zone) && zone != null;
    }

    public ERarity RollRarity()
    {
        ERarity rarity = odds.Roll();
        if (debugRolls) Debug.Log($"[LootRarityZone] {gameObject.scene.name}: rolled {rarity}", this);
        return rarity;
    }

    public bool ShouldSkipDrop(object rolledItem)
    {
        if (rolledItem == null || worseThanOwnedNoDropChance <= 0f) return false;

        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null) return false;

        if (!ItemActionFactory.IsWorseThanOwned(rolledItem, player.Inventory, player.Equipment)) return false;

        bool skip = Random.value < worseThanOwnedNoDropChance;
        if (debugRolls) Debug.Log($"[LootRarityZone] {gameObject.scene.name}: drop is worse than owned gear -> {(skip ? "skipped" : "dropped anyway")}", this);
        return skip;
    }
}