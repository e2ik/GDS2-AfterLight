using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MusicIntensityZone : MonoBehaviour
{
    [SerializeField] private bool unlockDynamicMusic = false;
    [SerializeField] private bool applyIntensityLimits = true;
    [SerializeField] private float minimumIntensity = 0f;
    [SerializeField] private float maximumIntensity = 99f;

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
        bool entered = inside && !playerInside;
        playerInside = inside;

        if (entered && unlockDynamicMusic) MusicIntensityDriver.UnlockIntensity();

        if (inside && applyIntensityLimits) MusicIntensityDriver.SetIntensityZone(this, minimumIntensity, maximumIntensity);
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