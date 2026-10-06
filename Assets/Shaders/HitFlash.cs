using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HitFlash : MonoBehaviour
{
    public enum PixelScale { Quarter, Third, Half, Full, Double, Triple, Quadruple }
    public enum FlashMode { Fill, Edge }

    [SerializeField] private Material flashMaterial;
    [SerializeField, ColorUsage(false, true)] private Color flashColor = Color.white;
    [SerializeField, Min(0f)] private float intensity = 2f;
    [SerializeField, Min(0.01f)] private float duration = 0.15f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField, Range(0f, 1f)] private float checkerLowOpacity = 0.4f;
    [SerializeField] private PixelScale pixelSize = PixelScale.Full;
    [SerializeField] private FlashMode defaultMode = FlashMode.Fill;
    [SerializeField, Range(1, 4)] private int edgeWidth = 1;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private int sortingOrderOffset = 1;

    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int AmountId = Shader.PropertyToID("_Amount");
    private static readonly int CheckerLowId = Shader.PropertyToID("_CheckerLow");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int EdgeOnlyId = Shader.PropertyToID("_EdgeOnly");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");

    private SpriteRenderer source;
    private SpriteRenderer overlay;
    private MaterialPropertyBlock block;
    private Color activeColor;
    private float activeIntensity;
    private FlashMode activeMode;
    private float timer;

    public bool IsFlashing => timer > 0f;

    private void Awake()
    {
        source = GetComponent<SpriteRenderer>();
        block = new MaterialPropertyBlock();

        GameObject child = new GameObject("HitFlash");
        child.hideFlags = HideFlags.HideAndDontSave;
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;

        overlay = child.AddComponent<SpriteRenderer>();
        overlay.sharedMaterial = flashMaterial;
        overlay.enabled = false;
    }

    private void OnDisable()
    {
        timer = 0f;
        if (overlay != null) overlay.enabled = false;
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.gameObject);
    }

    public void Flash() => Flash(flashColor, intensity, defaultMode);

    public void Flash(Color color) => Flash(color, intensity, defaultMode);

    public void Flash(Color color, float flashIntensity) => Flash(color, flashIntensity, defaultMode);

    public void Flash(Color color, float flashIntensity, FlashMode mode)
    {
        if (overlay == null || flashMaterial == null) return;

        activeColor = color;
        activeIntensity = flashIntensity;
        activeMode = mode;
        timer = duration;
        Sync(1f);
    }

    private void LateUpdate()
    {
        if (overlay == null) return;

        if (timer <= 0f)
        {
            if (overlay.enabled) overlay.enabled = false;
            return;
        }

        timer -= useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = 1f - Mathf.Clamp01(timer / duration);
        Sync(timer > 0f ? Mathf.Clamp01(fadeCurve.Evaluate(t)) : 0f);
    }

    private void Sync(float amount)
    {
        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder + sortingOrderOffset;
        overlay.drawMode = source.drawMode;
        if (source.drawMode != SpriteDrawMode.Simple) overlay.size = source.size;
        overlay.color = new Color(1f, 1f, 1f, source.color.a);
        overlay.enabled = amount > 0f && source.enabled && source.sprite != null;

        overlay.GetPropertyBlock(block);
        block.SetColor(FlashColorId, activeColor);
        block.SetFloat(IntensityId, activeIntensity);
        block.SetFloat(AmountId, amount);
        block.SetFloat(CheckerLowId, checkerLowOpacity);
        block.SetFloat(PixelSizeId, PixelSizeValue(pixelSize));
        block.SetFloat(EdgeOnlyId, activeMode == FlashMode.Edge ? 1f : 0f);
        block.SetFloat(EdgeWidthId, edgeWidth);
        overlay.SetPropertyBlock(block);
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
}