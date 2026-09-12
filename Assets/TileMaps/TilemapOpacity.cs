using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
[RequireComponent(typeof(Tilemap))]
public class TilemapOpacity : MonoBehaviour
{
    [Range(0f, 1f)] public float opacity = 1f;
    public Color colorOverlay = Color.white;

    private Tilemap tilemap;

    private void OnValidate()
    {
        if (tilemap == null) tilemap = GetComponent<Tilemap>();
        ApplyColor();
    }

    private void ApplyColor()
    {
        Color c = colorOverlay;
        c.a = opacity;
        tilemap.color = c;
    }
}