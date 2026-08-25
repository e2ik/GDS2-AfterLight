using UnityEngine;

public class SpriteOutlineToggle : MonoBehaviour
{
    [Header("Default Outline Settings")]
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineThickness = 1f;

    private SpriteRenderer[] spriteRenderers;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        propertyBlock = new MaterialPropertyBlock();
    }

    public void SetOutline(bool enabled)
    {
        SetOutline(enabled, outlineColor, outlineThickness);
    }

    public void SetOutline(bool enabled, Color color, float thickness)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0) return;

        foreach (var sr in spriteRenderers)
        {
            if (sr == null) continue;

            sr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
            propertyBlock.SetColor(OutlineColorID, color);
            propertyBlock.SetFloat(OutlineThicknessID, thickness);
            sr.SetPropertyBlock(propertyBlock);
        }
    }

    public Color DefaultColor => outlineColor;
    public float DefaultThickness => outlineThickness;
}