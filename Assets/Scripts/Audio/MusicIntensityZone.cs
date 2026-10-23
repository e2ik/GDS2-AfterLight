using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MusicIntensityZone : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float minimumIntensity = 0.2f;
    [SerializeField, Range(0f, 1f)] private float maximumIntensity = 1f;

    private Collider2D zone;
    private bool playerInside;

    private void Awake()
    {
        zone = GetComponent<Collider2D>();
    }

    private void OnDisable()
    {
        playerInside = false;
        MusicIntensityDriver.ClearIntensityZone(this);
    }

    private void Update()
    {
        bool inside = IsPlayerInside();
        if (inside == playerInside)
        {
            if (inside) MusicIntensityDriver.SetIntensityZone(this, minimumIntensity, maximumIntensity);
            return;
        }

        playerInside = inside;

        if (inside) MusicIntensityDriver.SetIntensityZone(this, minimumIntensity, maximumIntensity);
        else MusicIntensityDriver.ClearIntensityZone(this);
    }

    private bool IsPlayerInside()
    {
        if (zone == null || !zone.enabled) return false;

        Player player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (player == null || player.Controller == null) return false;

        return zone.OverlapPoint(player.Controller.BodyBounds.center);
    }
}
