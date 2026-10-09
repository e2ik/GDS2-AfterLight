using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class MultiplyOutline : MonoBehaviour
{
    public enum PixelScale { Quarter, Third, Half, Full, Double, Triple, Quadruple }

    [SerializeField] private Material outlineMaterial;
    [SerializeField] private Color tint = Color.white;
    [SerializeField, Range(0f, 1f)] private float strength = 0.5f;
    [SerializeField, Range(0f, 1f)] private float falloff = 0.5f;
    [SerializeField, Range(1, 8)] private int rings = 3;
    [SerializeField] private PixelScale pixelSize = PixelScale.Full;
    [SerializeField, Range(0f, 1f)] private float checkerContrast = 1f;

    [Header("Line")]
    [SerializeField] private bool showLine = true;
    [SerializeField] private Color lineColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float lineStrength = 1f;
    [SerializeField] private bool lineDiagonals = true;

    [SerializeField] private int sortingOrderOffset = -1;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int RingsId = Shader.PropertyToID("_Rings");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int CheckerId = Shader.PropertyToID("_Checker");
    private static readonly int LineEnabledId = Shader.PropertyToID("_LineEnabled");
    private static readonly int LineColorId = Shader.PropertyToID("_LineColor");
    private static readonly int LineStrengthId = Shader.PropertyToID("_LineStrength");
    private static readonly int LineDiagonalsId = Shader.PropertyToID("_LineDiagonals");

    private const string ChildName = "MultiplyOutline";

    private SpriteRenderer source;
    private SpriteRenderer outline;
    private MaterialPropertyBlock block;

    private void OnEnable()
    {
        source = GetComponent<SpriteRenderer>();
        block ??= new MaterialPropertyBlock();
        EnsureOutline();
        Sync();
    }

    private void OnDisable()
    {
        if (outline == null) return;

        if (Application.isPlaying) Destroy(outline.gameObject);
        else DestroyImmediate(outline.gameObject);

        outline = null;
    }

    private void EnsureOutline()
    {
        if (outline != null) return;

        Transform existing = transform.Find(ChildName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        GameObject child = new GameObject(ChildName);
        child.hideFlags = HideFlags.HideAndDontSave;
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;

        outline = child.AddComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        Sync();
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled && outline != null) Sync();
    }

    private static float PixelSizeValue(PixelScale scale)
    {
        switch (scale)
        {
            case PixelScale.Quarter: return 0.25f;
            case PixelScale.Third: return 1f / 3f;
            case PixelScale.Half: return 0.5f;
            case PixelScale.Double: return 2f;
            case PixelScale.Triple: return 3f;
            case PixelScale.Quadruple: return 4f;
            default: return 1f;
        }
    }

    private void Sync()
    {
        if (outline == null || source == null) return;

        outline.sharedMaterial = outlineMaterial;
        outline.sprite = source.sprite;
        outline.flipX = source.flipX;
        outline.flipY = source.flipY;
        outline.enabled = source.enabled && source.sprite != null && outlineMaterial != null;
        outline.sortingLayerID = source.sortingLayerID;
        outline.sortingOrder = source.sortingOrder + sortingOrderOffset;
        outline.drawMode = source.drawMode;
        if (source.drawMode != SpriteDrawMode.Simple) outline.size = source.size;
        outline.color = new Color(1f, 1f, 1f, source.color.a);

        block ??= new MaterialPropertyBlock();
        outline.GetPropertyBlock(block);
        block.SetColor(OutlineColorId, tint);
        block.SetFloat(StrengthId, strength);
        block.SetFloat(FalloffId, falloff);
        block.SetFloat(RingsId, rings);
        block.SetFloat(PixelSizeId, PixelSizeValue(pixelSize));
        block.SetFloat(CheckerId, checkerContrast);
        block.SetFloat(LineEnabledId, showLine ? 1f : 0f);
        block.SetColor(LineColorId, lineColor);
        block.SetFloat(LineStrengthId, lineStrength);
        block.SetFloat(LineDiagonalsId, lineDiagonals ? 1f : 0f);
        outline.SetPropertyBlock(block);
    }
}