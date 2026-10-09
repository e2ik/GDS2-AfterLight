using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class MultiplyOutline : MonoBehaviour
{
    public enum PixelScale { Quarter, Third, Half, Full, Double, Triple, Quadruple }

    [System.Serializable]
    public class GlowStyle
    {
        public bool enabled = true;
        [ColorUsage(false, true)] public Color color = Color.white;
        [Range(0f, 10f)] public float intensity = 2f;
        [Range(0f, 1f)] public float checkerLow = 0.5f;
        [Range(1, 4)] public int checkerSize = 1;
    }

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

    [Header("Color Glow")]
    [SerializeField] private bool glowColor = false;
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Color matchColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float matchTolerance = 0.1f;
    [SerializeField, ColorUsage(false, true)] private Color replaceColor = Color.white;
    [SerializeField, Range(0f, 10f)] private float glowIntensity = 2f;
    [SerializeField, Range(0f, 1f)] private float glowCheckerLow = 0.5f;
    [SerializeField, Range(1, 4)] private int glowCheckerSize = 1;
    [SerializeField, Min(0f)] private float glowFadeDuration = 0.08f;
    [SerializeField] private int glowSortingOrderOffset = 1;

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
    private static readonly int MatchColorId = Shader.PropertyToID("_MatchColor");
    private static readonly int ToleranceId = Shader.PropertyToID("_Tolerance");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int GlowCheckerLowId = Shader.PropertyToID("_CheckerLow");
    private static readonly int GlowCheckerSizeId = Shader.PropertyToID("_CheckerSize");
    private static readonly int GlowFadeId = Shader.PropertyToID("_GlowFade");

    private const string ChildName = "MultiplyOutline";
    private const string GlowChildName = "MultiplyOutlineGlow";

    private SpriteRenderer source;
    private SpriteRenderer outline;
    private SpriteRenderer glow;
    private MaterialPropertyBlock block;
    private MaterialPropertyBlock glowBlock;
    private bool glowActive = true;
    private float glowFade = 1f;
    private GlowStyle activeStyle;

    public void SetColorGlowActive(bool active) => glowActive = active;

    public void SetColorGlow(GlowStyle style)
    {
        bool active = style != null && style.enabled;
        glowActive = active;
        if (active) activeStyle = style;
    }

    public void ClearColorGlowStyle() => activeStyle = null;
    public void GlowOn() => SetColorGlowActive(true);
    public void GlowOff() => SetColorGlowActive(false);

    private void OnEnable()
    {
        source = GetComponent<SpriteRenderer>();
        block ??= new MaterialPropertyBlock();
        glowBlock ??= new MaterialPropertyBlock();
        EnsureOutline();
        Sync();
    }

    private void OnDisable()
    {
        DestroyChild(ref outline);
        DestroyChild(ref glow);
    }

    private static void DestroyChild(ref SpriteRenderer renderer)
    {
        if (renderer == null) return;

        if (Application.isPlaying) Destroy(renderer.gameObject);
        else DestroyImmediate(renderer.gameObject);

        renderer = null;
    }

    private void EnsureOutline()
    {
        if (outline == null) outline = CreateChild(ChildName);
        if (glow == null) glow = CreateChild(GlowChildName);
    }

    private SpriteRenderer CreateChild(string childName)
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        GameObject child = new GameObject(childName);
        child.hideFlags = HideFlags.HideAndDontSave;
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;

        return child.AddComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        float target = glowActive ? 1f : 0f;
        glowFade = !Application.isPlaying || glowFadeDuration <= 0f
            ? target
            : Mathf.MoveTowards(glowFade, target, Time.deltaTime / glowFadeDuration);

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

        SyncGlow();
    }

    private void SyncGlow()
    {
        if (glow == null) return;

        glow.sharedMaterial = glowMaterial;
        glow.enabled = glowColor && glowFade > 0f && source.enabled && source.sprite != null && glowMaterial != null;
        if (!glow.enabled) return;

        glow.sprite = source.sprite;
        glow.flipX = source.flipX;
        glow.flipY = source.flipY;
        glow.sortingLayerID = source.sortingLayerID;
        glow.sortingOrder = source.sortingOrder + glowSortingOrderOffset;
        glow.drawMode = source.drawMode;
        if (source.drawMode != SpriteDrawMode.Simple) glow.size = source.size;
        glow.color = new Color(1f, 1f, 1f, source.color.a);

        glowBlock ??= new MaterialPropertyBlock();
        glow.GetPropertyBlock(glowBlock);
        glowBlock.SetColor(MatchColorId, matchColor);
        glowBlock.SetFloat(ToleranceId, matchTolerance);
        glowBlock.SetColor(GlowColorId, activeStyle != null ? activeStyle.color : replaceColor);
        glowBlock.SetFloat(GlowIntensityId, activeStyle != null ? activeStyle.intensity : glowIntensity);
        glowBlock.SetFloat(GlowCheckerLowId, activeStyle != null ? activeStyle.checkerLow : glowCheckerLow);
        glowBlock.SetFloat(GlowCheckerSizeId, activeStyle != null ? activeStyle.checkerSize : glowCheckerSize);
        glowBlock.SetFloat(GlowFadeId, glowFade);
        glow.SetPropertyBlock(glowBlock);
    }
}