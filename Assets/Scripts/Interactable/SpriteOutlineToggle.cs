using UnityEngine;

public class SpriteOutlineToggle : MonoBehaviour
{
    [Header("Outline Defaults")]
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineThickness = 1f;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    public void SetOutline(bool enabled)
    {
        SetOutline(enabled, outlineColor, outlineThickness);
    }

    public void SetOutline(bool enabled, Color color, float thickness)
    {
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
        propertyBlock.SetColor(OutlineColorID, color);
        propertyBlock.SetFloat(OutlineThicknessID, thickness);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    public Color DefaultColor => outlineColor;
    public float DefaultThickness => outlineThickness;
}