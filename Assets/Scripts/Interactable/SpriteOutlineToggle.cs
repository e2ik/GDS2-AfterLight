using System.Collections;
using UnityEngine;

public class SpriteOutlineToggle : MonoBehaviour
{
    [Header("Default Outline Settings")]
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineThickness = 1f;

    [Header("Hover Highlight")]
    [SerializeField] private bool pulseOnHighlight = true;
    [SerializeField, Min(0.01f)] private float highlightPulseSpeed = 3f;
    [SerializeField, Range(0f, 1f)] private float highlightMinAlpha = 0.2f;
    [SerializeField, Range(0f, 1f)] private float highlightMaxAlpha = 1f;

    [Header("Default Flash Settings")]
    [SerializeField, Min(1)] private int flashCount = 3;
    [SerializeField, Min(0f)] private float flashInterval = 0.15f;

    private SpriteRenderer[] spriteRenderers;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine flashRoutine;

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
        StopFlash();
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

    public void BeginHighlight()
    {
        StopFlash();

        if (pulseOnHighlight)
            flashRoutine = StartCoroutine(HighlightPulseRoutine());
        else
            SetOutline(true, outlineColor, outlineThickness);
    }

    public void EndHighlight()
    {
        StopFlash();
        SetOutline(false, outlineColor, outlineThickness);
    }

    private IEnumerator HighlightPulseRoutine()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * highlightPulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(highlightMinAlpha, highlightMaxAlpha, t);

            Color pulsedColor = outlineColor;
            pulsedColor.a = alpha;

            SetOutline(true, pulsedColor, outlineThickness);
            yield return null;
        }
    }

    public void FlashOutline()
    {
        FlashOutline(flashCount, flashInterval, outlineColor, outlineThickness);
    }

    public void FlashOutline(int count, float interval, Color? color = null, float? thickness = null)
    {
        StopFlash();
        flashRoutine = StartCoroutine(FlashRoutine(count, interval, color ?? outlineColor, thickness ?? outlineThickness));
    }

    public void StopFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
    }

    private IEnumerator FlashRoutine(int count, float interval, Color color, float thickness)
    {
        for (int i = 0; i < count; i++)
        {
            SetOutline(true, color, thickness);
            if (interval > 0f) yield return new WaitForSeconds(interval);

            SetOutline(false, color, thickness);
            if (interval > 0f) yield return new WaitForSeconds(interval);
        }

        flashRoutine = null;
    }

    public Color DefaultColor => outlineColor;
    public float DefaultThickness => outlineThickness;
}