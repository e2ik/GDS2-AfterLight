using UnityEngine;

public class DeathScreenController : MonoBehaviour
{
    public void OnRespawnClicked()
    {
        PlayerStats playerStats = FindFirstObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.OnRespawnButtonPressed();
        }
        else
        {
            Debug.LogWarning("[DeathScreenController] No PlayerStats found in scene; cannot respawn.");
        }
    }

    public void OnReturnToTitleClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToTitle();
        }
    }
}