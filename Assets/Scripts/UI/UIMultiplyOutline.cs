using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class UIMultiplyOutline : MonoBehaviour
{
    public enum PixelScale { Quarter, Half, Full, Double }

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

    private const string ChildName = "UIMultiplyOutline";

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

    private Image source;
    private Image outline;
    private RectTransform outlineRect;
    private Material materialInstance;

    private void OnEnable()
    {
        source = GetComponent<Image>();
        EnsureOutline();
        Sync();
    }

    private void OnDisable()
    {
        if (outline != null)
        {
            if (Application.isPlaying) Destroy(outline.gameObject);
            else DestroyImmediate(outline.gameObject);
            outline = null;
            outlineRect = null;
        }

        if (materialInstance != null)
        {
            if (Application.isPlaying) Destroy(materialInstance);
            else DestroyImmediate(materialInstance);
            materialInstance = null;
        }
    }

    private void OnValidate()
    {
        if (isActiveAndEnabled && outline != null) Sync();
    }

    private void LateUpdate()
    {
        Sync();
    }

    private void EnsureOutline()
    {
        if (outline != null || transform.parent == null) return;

        Transform existing = transform.parent.Find(ChildName + "_" + name);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        GameObject child = new GameObject(ChildName + "_" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.hideFlags = HideFlags.HideAndDontSave;
        child.layer = gameObject.layer;

        outlineRect = child.GetComponent<RectTransform>();
        outlineRect.SetParent(transform.parent, false);

        outline = child.GetComponent<Image>();
        outline.raycastTarget = false;
    }

    private void Sync()
    {
        if (source == null) return;
        EnsureOutline();
        if (outline == null || outlineMaterial == null) return;

        if (materialInstance == null || materialInstance.shader != outlineMaterial.shader)
        {
            if (materialInstance != null)
            {
                if (Application.isPlaying) Destroy(materialInstance);
                else DestroyImmediate(materialInstance);
            }
            materialInstance = new Material(outlineMaterial) { name = outlineMaterial.name + " (Instance)", hideFlags = HideFlags.HideAndDontSave };
        }

        RectTransform sourceRect = source.rectTransform;
        if (outlineRect.parent != sourceRect.parent) outlineRect.SetParent(sourceRect.parent, false);

        int targetIndex = Mathf.Max(0, sourceRect.GetSiblingIndex() - (outlineRect.GetSiblingIndex() < sourceRect.GetSiblingIndex() ? 1 : 0));
        if (outlineRect.GetSiblingIndex() != targetIndex) outlineRect.SetSiblingIndex(targetIndex);

        outlineRect.anchorMin = sourceRect.anchorMin;
        outlineRect.anchorMax = sourceRect.anchorMax;
        outlineRect.pivot = sourceRect.pivot;
        outlineRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
        outlineRect.sizeDelta = sourceRect.sizeDelta;
        outlineRect.localRotation = sourceRect.localRotation;
        outlineRect.localScale = sourceRect.localScale;

        outline.gameObject.SetActive(source.isActiveAndEnabled && source.sprite != null);
        outline.sprite = source.sprite;
        outline.type = source.type;
        outline.preserveAspect = source.preserveAspect;
        outline.color = new Color(1f, 1f, 1f, source.color.a);
        outline.material = materialInstance;

        materialInstance.SetColor(OutlineColorId, tint);
        materialInstance.SetFloat(StrengthId, strength);
        materialInstance.SetFloat(FalloffId, falloff);
        materialInstance.SetFloat(RingsId, rings);
        materialInstance.SetFloat(PixelSizeId, PixelSizeValue(pixelSize));
        materialInstance.SetFloat(CheckerId, checkerContrast);
        materialInstance.SetFloat(LineEnabledId, showLine ? 1f : 0f);
        materialInstance.SetColor(LineColorId, lineColor);
        materialInstance.SetFloat(LineStrengthId, lineStrength);
        materialInstance.SetFloat(LineDiagonalsId, lineDiagonals ? 1f : 0f);
    }

    private static float PixelSizeValue(PixelScale scale)
    {
        switch (scale)
        {
            case PixelScale.Quarter: return 0.25f;
            case PixelScale.Half: return 0.5f;
            case PixelScale.Double: return 2f;
            default: return 1f;
        }
    }
}
