using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
public class ParticleGlow : MonoBehaviour
{
    [SerializeField] private Material glowMaterial;
    [SerializeField, Min(0f)] private float intensity = 2f;
    [SerializeField] private bool setStartColor;
    [SerializeField] private Color color = Color.white;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private ParticleSystem ps;
    private ParticleSystemRenderer psRenderer;
    private MaterialPropertyBlock block;

    private void OnEnable()
    {
        Apply();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Apply();
    }
#endif

    public void SetIntensity(float newIntensity)
    {
        intensity = newIntensity;
        Apply();
    }

    public void SetColor(Color newColor)
    {
        setStartColor = true;
        color = newColor;
        Apply();
    }

    public void Apply()
    {
        if (ps == null) ps = GetComponent<ParticleSystem>();
        if (psRenderer == null) psRenderer = GetComponent<ParticleSystemRenderer>();
        if (ps == null || psRenderer == null) return;

        if (glowMaterial != null && psRenderer.sharedMaterial != glowMaterial)
            psRenderer.sharedMaterial = glowMaterial;

        Material material = psRenderer.sharedMaterial;
        if (material == null || !material.HasProperty(EmissionColorId)) return;

        if (!material.IsKeywordEnabled("_EMISSION"))
            Debug.LogWarning($"[ParticleGlow] Emission isn't ticked on '{material.name}', so '{name}' won't glow.", this);

        if (setStartColor)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = color;
        }

        Color baseColor = setStartColor ? color : GetStartColor();
        Color glow = new Color(baseColor.r, baseColor.g, baseColor.b, 1f) * intensity;
        glow.a = 1f;

        if (block == null) block = new MaterialPropertyBlock();
        psRenderer.GetPropertyBlock(block);
        block.SetColor(EmissionColorId, glow);
        psRenderer.SetPropertyBlock(block);
    }

    private Color GetStartColor()
    {
        ParticleSystem.MinMaxGradient startColor = ps.main.startColor;

        switch (startColor.mode)
        {
            case ParticleSystemGradientMode.TwoColors:
                return Color.Lerp(startColor.colorMin, startColor.colorMax, 0.5f);
            case ParticleSystemGradientMode.Gradient:
            case ParticleSystemGradientMode.RandomColor:
                return startColor.gradient != null ? startColor.gradient.Evaluate(0.5f) : Color.white;
            case ParticleSystemGradientMode.TwoGradients:
                return startColor.gradientMax != null ? startColor.gradientMax.Evaluate(0.5f) : Color.white;
            default:
                return startColor.color;
        }
    }
}