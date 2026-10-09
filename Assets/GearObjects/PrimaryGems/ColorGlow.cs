using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ColorGlow : MonoBehaviour
{
    [System.Serializable]
    public class GlowStyle
    {
        public bool enabled = true;
        [ColorUsage(false, true)] public Color color = Color.white;
        [Range(0f, 10f)] public float intensity = 2f;
        [Range(0f, 1f)] public float checkerLow = 0.5f;
        [Range(1, 4)] public int checkerSize = 1;
    }

    [SerializeField] private Material glowMaterial;
    [SerializeField] private bool startActive = true;

    [Header("Match")]
    [SerializeField] private Color matchColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float matchTolerance = 0.1f;

    [Header("Glow")]
    [SerializeField, ColorUsage(false, true)] private Color glowColor = Color.white;
    [SerializeField, Range(0f, 10f)] private float intensity = 2f;
    [SerializeField, Range(0f, 1f)] private float checkerLow = 0.5f;
    [SerializeField, Range(1, 4)] private int checkerSize = 1;

    [Header("Fade & Sorting")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.08f;
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private int sortingOrderOffset = 1;

    private static readonly int MatchColorId = Shader.PropertyToID("_MatchColor");
    private static readonly int ToleranceId = Shader.PropertyToID("_Tolerance");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int CheckerLowId = Shader.PropertyToID("_CheckerLow");
    private static readonly int CheckerSizeId = Shader.PropertyToID("_CheckerSize");
    private static readonly int GlowFadeId = Shader.PropertyToID("_GlowFade");

    private const string ChildName = "ColorGlow";

    private SpriteRenderer source;
    private SpriteRenderer overlay;
    private MaterialPropertyBlock block;
    private GlowStyle activeStyle;
    private bool active;
    private float fade;

    public bool IsActive => active;

    private void Awake()
    {
        active = startActive;
        fade = active ? 1f : 0f;
    }

    private void OnEnable()
    {
        source = GetComponent<SpriteRenderer>();
        block ??= new MaterialPropertyBlock();
        if (!Application.isPlaying) active = startActive;
        EnsureOverlay();
        Sync();
    }

    private void OnDisable()
    {
        if (overlay == null) return;

        if (Application.isPlaying) Destroy(overlay.gameObject);
        else DestroyImmediate(overlay.gameObject);

        overlay = null;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) active = startActive;
        if (isActiveAndEnabled && overlay != null) Sync();
    }

    public void SetActive(bool value) => active = value;
    public void GlowOn() => active = true;
    public void GlowOff() => active = false;
    public void Toggle() => active = !active;

    public void SetStyle(GlowStyle style)
    {
        bool on = style != null && style.enabled;
        active = on;
        if (on) activeStyle = style;
    }

    public void ClearStyle() => activeStyle = null;

    public void SetGlowColor(Color color) => glowColor = color;
    public void SetIntensity(float value) => intensity = Mathf.Max(0f, value);

    private void EnsureOverlay()
    {
        if (overlay != null) return;

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

        overlay = child.AddComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        float target = active ? 1f : 0f;
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        fade = !Application.isPlaying || fadeDuration <= 0f
            ? target
            : Mathf.MoveTowards(fade, target, delta / fadeDuration);

        Sync();
    }

    private void Sync()
    {
        if (overlay == null || source == null) return;

        overlay.sharedMaterial = glowMaterial;
        overlay.enabled = fade > 0f && source.enabled && source.sprite != null && glowMaterial != null;
        if (!overlay.enabled) return;

        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder + sortingOrderOffset;
        overlay.drawMode = source.drawMode;
        if (source.drawMode != SpriteDrawMode.Simple) overlay.size = source.size;
        overlay.color = new Color(1f, 1f, 1f, source.color.a);

        block ??= new MaterialPropertyBlock();
        overlay.GetPropertyBlock(block);
        block.SetColor(MatchColorId, matchColor);
        block.SetFloat(ToleranceId, matchTolerance);
        block.SetColor(GlowColorId, activeStyle != null ? activeStyle.color : glowColor);
        block.SetFloat(IntensityId, activeStyle != null ? activeStyle.intensity : intensity);
        block.SetFloat(CheckerLowId, activeStyle != null ? activeStyle.checkerLow : checkerLow);
        block.SetFloat(CheckerSizeId, activeStyle != null ? activeStyle.checkerSize : checkerSize);
        block.SetFloat(GlowFadeId, fade);
        overlay.SetPropertyBlock(block);
    }
}