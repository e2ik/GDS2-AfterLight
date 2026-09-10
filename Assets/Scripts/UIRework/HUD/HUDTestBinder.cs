using UnityEngine;

public class HUDTestBinder : MonoBehaviour
{
    [SerializeField] private GameUI.PlayerHUD hud;
    [SerializeField] private PlayerStats testStats;

    private void Start()
    {
        hud.Bind(testStats, null);
    }

    [ContextMenu("Test Damage (10)")]
    private void TestDamage()
    {
        testStats.TakeDamage(10f);
    }

    [ContextMenu("Test Heal (10)")]
    private void TestHeal()
    {
        testStats.Heal(10f);
    }
}