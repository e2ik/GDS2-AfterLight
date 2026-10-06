using UnityEngine;

[RequireComponent(typeof(Chest))]
public class ChestShine : MonoBehaviour
{
    public enum OutlineSide { Inside, Outside }

    [Header("References")]
    [SerializeField] private SpriteRenderer chestRenderer;
    [SerializeField] private Material shineMaterial;
    [SerializeField] private int sortingOrderOffset = 1;

    [Header("Color")]
    [SerializeField] private bool overrideColor = false;
    [SerializeField, ColorUsage(false, true)] private Color shineColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField, Range(1f, 10f)] private float intensity = 2f;
    [SerializeField, Range(0f, 1f)] private float additive = 0.1f;

    [Header("Shine")]
    [SerializeField, Range(0f, 1f)] private float fillAmount = 1f;
    [SerializeField, Range(0f, 16f)] private float coreWidth = 1f;
    [SerializeField, Range(0f, 16f)] private float fringeWidth = 2f;
    [SerializeField] private bool flipDirection = false;
    [SerializeField, Range(0f, 1f)] private float checkerLow = 0.4f;
    [SerializeField, Range(1, 4)] private int checkerSize = 1;

    [Header("Outline")]
    [SerializeField] private bool showOutline = true;
    [SerializeField] private OutlineSide outlineSide = OutlineSide.Inside;
    [SerializeField] private bool outlineAlwaysOn = false;
    [SerializeField, Range(0f, 1f)] private float outlineAmount = 1f;
    [SerializeField, Range(1, 4)] private int outlineWidth = 1;
    [SerializeField, Range(0f, 1f)] private float outlineAdditive = 0.5f;

    [Header("Timing")]
    [SerializeField] private float sweepFrom = -32f;
    [SerializeField] private float sweepTo = 32f;
    [SerializeField, Min(0.01f)] private float duration = 0.6f;
    [SerializeField, Min(0f)] private float delay = 2f;
    [SerializeField] private float timeOffset = 0f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;

    private static readonly int ShineFadeId = Shader.PropertyToID("_ShineFade");
    private static readonly int OverlayOnlyId = Shader.PropertyToID("_OverlayOnly");
    private static readonly int ShineColorId = Shader.PropertyToID("_ShineColor");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int AdditiveId = Shader.PropertyToID("_Additive");
    private static readonly int PixelsPerUnitId = Shader.PropertyToID("_PixelsPerUnit");
    private static readonly int FlipDirectionId = Shader.PropertyToID("_FlipDirection");
    private static readonly int WidthId = Shader.PropertyToID("_Width");
    private static readonly int FringeId = Shader.PropertyToID("_Fringe");
    private static readonly int CheckerLowId = Shader.PropertyToID("_CheckerLow");
    private static readonly int CheckerSizeId = Shader.PropertyToID("_CheckerSize");
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int EdgeAmountId = Shader.PropertyToID("_EdgeAmount");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int EdgeAdditiveId = Shader.PropertyToID("_EdgeAdditive");
    private static readonly int EdgeAlwaysId = Shader.PropertyToID("_EdgeAlways");
    private static readonly int EdgeSideId = Shader.PropertyToID("_EdgeSide");
    private static readonly int SweepFromId = Shader.PropertyToID("_SweepFrom");
    private static readonly int SweepToId = Shader.PropertyToID("_SweepTo");
    private static readonly int DurationId = Shader.PropertyToID("_Duration");
    private static readonly int DelayId = Shader.PropertyToID("_Delay");
    private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");

    private Chest chest;
    private SpriteRenderer overlay;
    private MaterialPropertyBlock block;
    private float fade = 1f;
    private bool lastClosed;

    private void Awake()
    {
        chest = GetComponent<Chest>();
        if (chestRenderer == null) chestRenderer = GetComponentInChildren<SpriteRenderer>();
        block = new MaterialPropertyBlock();

        if (chestRenderer == null || shineMaterial == null) return;

        GameObject child = new GameObject("ChestShine");
        child.hideFlags = HideFlags.HideAndDontSave;
        child.transform.SetParent(chestRenderer.transform, false);
        child.layer = chestRenderer.gameObject.layer;

        overlay = child.AddComponent<SpriteRenderer>();
        overlay.sharedMaterial = shineMaterial;
    }

    private void Start()
    {
        lastClosed = chest.CanInteract;
        fade = lastClosed ? 1f : 0f;
    }

    private void OnDestroy()
    {
        if (overlay != null) Destroy(overlay.gameObject);
    }

    private void LateUpdate()
    {
        if (overlay == null) return;

        bool closed = chest.CanInteract;

        if (closed != lastClosed)
        {
            lastClosed = closed;
            if (closed) fade = 1f;
            else if (fadeOutDuration <= 0f) fade = 0f;
        }

        if (!closed && fade > 0f)
            fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime / fadeOutDuration);

        Sync();
    }

    private void Sync()
    {
        overlay.enabled = fade > 0f && chestRenderer.enabled && chestRenderer.sprite != null;
        if (!overlay.enabled) return;

        Sprite sprite = chestRenderer.sprite;
        overlay.sprite = sprite;
        overlay.flipX = chestRenderer.flipX;
        overlay.flipY = chestRenderer.flipY;
        overlay.sortingLayerID = chestRenderer.sortingLayerID;
        overlay.sortingOrder = chestRenderer.sortingOrder + sortingOrderOffset;
        overlay.drawMode = chestRenderer.drawMode;
        if (chestRenderer.drawMode != SpriteDrawMode.Simple) overlay.size = chestRenderer.size;
        overlay.color = chestRenderer.color;

        overlay.GetPropertyBlock(block);

        block.SetFloat(ShineFadeId, fade);
        block.SetFloat(OverlayOnlyId, 1f);
        if (overrideColor) block.SetColor(ShineColorId, shineColor);
        block.SetFloat(IntensityId, intensity);
        block.SetFloat(AdditiveId, additive);
        block.SetFloat(PixelsPerUnitId, sprite.pixelsPerUnit);

        block.SetFloat(FillAmountId, fillAmount);
        block.SetFloat(WidthId, coreWidth);
        block.SetFloat(FringeId, fringeWidth);
        block.SetFloat(FlipDirectionId, flipDirection ? 1f : 0f);
        block.SetFloat(CheckerLowId, checkerLow);
        block.SetFloat(CheckerSizeId, checkerSize);

        block.SetFloat(EdgeAmountId, showOutline ? outlineAmount : 0f);
        block.SetFloat(EdgeSideId, outlineSide == OutlineSide.Outside ? 1f : 0f);
        block.SetFloat(EdgeAlwaysId, outlineAlwaysOn ? 1f : 0f);
        block.SetFloat(EdgeWidthId, outlineWidth);
        block.SetFloat(EdgeAdditiveId, outlineAdditive);

        block.SetFloat(SweepFromId, sweepFrom);
        block.SetFloat(SweepToId, sweepTo);
        block.SetFloat(DurationId, duration);
        block.SetFloat(DelayId, delay);
        block.SetFloat(TimeOffsetId, timeOffset);

        overlay.SetPropertyBlock(block);
    }
}