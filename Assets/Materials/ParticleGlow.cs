using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
public class ParticleGlow : MonoBehaviour
{
    [SerializeField] private Material glowMaterial;
    [SerializeField, Min(0f)] private float intensity = 2f;
    [SerializeField] private bool setStartColor;
    [SerializeField] private Color color = Color.white;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

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
        if (material == null || !material.HasProperty(BaseColorId)) return;

        if (setStartColor)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = color;
        }

        Color boost = new Color(intensity, intensity, intensity, 1f);

        if (block == null) block = new MaterialPropertyBlock();
        psRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, boost);
        psRenderer.SetPropertyBlock(block);
    }
}