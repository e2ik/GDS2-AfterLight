using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ParallaxColorZone : MonoBehaviour
{
    [SerializeField] private bool useDefaultColors = false;
    [SerializeField] private List<ParallaxBackground.LayerColor> layerColors = new List<ParallaxBackground.LayerColor>();
    [SerializeField] private bool resetOnExit = false;
    [SerializeField] private bool instant = false;

    private int playerColliderCount;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        playerColliderCount++;
        if (playerColliderCount > 1) return;

        ParallaxBackground background = GetBackground();
        if (background == null) return;

        if (useDefaultColors) background.ResetLayerColors(instant);
        else background.SetLayerColors(layerColors, instant);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other)) return;

        playerColliderCount = Mathf.Max(0, playerColliderCount - 1);
        if (!resetOnExit || playerColliderCount > 0) return;

        ParallaxBackground background = GetBackground();
        if (background != null) background.ResetLayerColors(instant);
    }

    private void OnDisable()
    {
        playerColliderCount = 0;
    }

    private static bool IsPlayer(Collider2D other)
    {
        return other.GetComponentInParent<Player>() != null;
    }

    private static ParallaxBackground GetBackground()
    {
        return GameManager.Instance != null ? GameManager.Instance.Background : null;
    }
}
