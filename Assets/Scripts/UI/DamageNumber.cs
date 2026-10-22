using System.Collections.Generic;
using System.Text;
using Enemies;
using TMPro;
using UnityEngine;

public class DamageNumber : MonoBehaviour
{
    [System.Serializable]
    public class DamageStyle
    {
        public string format = "{amount}";
        public Color color = Color.white;
        public float scale = 1f;
        public Material material;
    }

    [SerializeField] private TMP_Text text;

    [Header("Glow")]
    [SerializeField, Min(0f)] private float glowIntensity = 1f;

    [Header("Styles")]
    [SerializeField] private DamageStyle normalStyle = new DamageStyle();
    [SerializeField] private DamageStyle critStyle = new DamageStyle { format = "{amount}!", color = new Color(1f, 0.85f, 0.2f), scale = 1.4f };
    [SerializeField] private DamageStyle skillStyle = new DamageStyle { color = new Color(0.7f, 0.45f, 1f), scale = 1.15f };
    [SerializeField] private DamageStyle dotStyle = new DamageStyle { color = new Color(0.85f, 0.25f, 0.25f), scale = 0.7f };
    [SerializeField] private DamageStyle reflectStyle = new DamageStyle { color = new Color(0.35f, 0.65f, 1f) };
    [SerializeField] private DamageStyle dodgeStyle = new DamageStyle { format = "Dodged", color = new Color(0.85f, 0.85f, 0.85f), scale = 0.9f };
    [SerializeField] private DamageStyle perfectDodgeStyle = new DamageStyle { format = "Perfect!", color = new Color(1f, 0.84f, 0f), scale = 1.2f };
    [SerializeField] private DamageStyle blockedStyle = new DamageStyle { format = "{blocked} Blocked", color = Color.white, scale = 0.7f };

    [Header("Resisted")]
    [SerializeField] private DamageStyle resistedStyle = new DamageStyle { format = "-{resisted} Resisted", color = Color.white, scale = 0.7f };
    [Header("Gamble Roll")]
    [SerializeField] private bool tintByRoll = true;
    [SerializeField] private Color lowRollColor = Color.gray;
    [SerializeField] private Color midRollColor = Color.white;
    [SerializeField] private Color highRollColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private float lowRollScale = 0.8f;
    [SerializeField] private float highRollScale = 1.3f;
    [SerializeField] private string niceSuffix = "\\nNICE";
    [SerializeField] private string[] niceVariants = { "NICE", "SWEET", "SLICK", "CLEAN", "SHARP", "CRISP", "SOLID", "NEAT" };
    [SerializeField] private string jackpotSuffix = "\\nJACKPOT!";
    [SerializeField] private bool rainbowJackpot = true;
    [SerializeField] private float rainbowSpeed = 2f;
    [SerializeField] private float rainbowCharOffset = 0.1f;
    [SerializeField] private string whiffSuffix = "\\nwhiff";

    [Header("Motion")]
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private Vector2 riseVelocity = new Vector2(0f, 1.5f);
    [SerializeField] private float randomSpreadX = 0.3f;
    [SerializeField] private AnimationCurve alphaOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve scaleOverLife = AnimationCurve.Constant(0f, 1f, 1f);

    private readonly StringBuilder sb = new StringBuilder();
    private Material defaultMaterial;
    private static readonly Dictionary<(Material, float), Material> glowMaterials = new Dictionary<(Material, float), Material>();
    private Vector3 initialScale;
    private Vector3 velocity;
    private Color baseColor;
    private float baseScale;
    private float age;
    private int rainbowStartIndex = -1;

    private void Awake()
    {
        if (text == null) text = GetComponentInChildren<TMP_Text>();
        if (text != null) defaultMaterial = text.fontSharedMaterial;
        initialScale = transform.localScale;
    }

    public void Show(DamageInfo info)
    {
        DamageStyle style = PickStyle(info);
        Color color = style.color;
        float scale = style.scale;

        sb.Clear();
        sb.Append(style.format);
        sb.Replace("{amount}", info.Amount.ToString());
        rainbowStartIndex = -1;

        if (info.HasRoll && (info.DamageType == EDamageType.Base || info.DamageType == EDamageType.Skill))
        {
            if (tintByRoll && !info.IsCrit && info.DamageType == EDamageType.Base)
                color = GetRollColor(info.RollQuality);

            if (tintByRoll)
                scale *= Mathf.Lerp(lowRollScale, highRollScale, info.RollQuality);
        }

        Begin(color, scale, style.material);
    }

    public void ShowBlocked(DamageInfo info)
    {
        sb.Clear();
        sb.Append(blockedStyle.format);
        sb.Replace("{blocked}", info.Blocked.ToString());
        sb.Replace("{amount}", info.Amount.ToString());
        rainbowStartIndex = -1;

        Begin(blockedStyle.color, blockedStyle.scale, blockedStyle.material);
    }

    public void ShowResisted(DamageInfo info)
    {
        sb.Clear();
        sb.Append(resistedStyle.format);
        sb.Replace("{resisted}", info.Resisted.ToString());
        sb.Replace("{amount}", info.Amount.ToString());
        rainbowStartIndex = -1;

        Begin(resistedStyle.color, resistedStyle.scale, resistedStyle.material);
    }

    public static bool HasRollText(ERollTier tier)
    {
        return tier == ERollTier.Jackpot || tier == ERollTier.Nice || tier == ERollTier.Whiff;
    }

    public void ShowRollTier(ERollTier tier)
    {
        string label;
        Color color;
        float scale;

        switch (tier)
        {
            case ERollTier.Jackpot:
                label = jackpotSuffix;
                color = highRollColor;
                scale = highRollScale;
                break;
            case ERollTier.Nice:
                label = niceVariants != null && niceVariants.Length > 0
                    ? niceVariants[Random.Range(0, niceVariants.Length)]
                    : niceSuffix;
                color = highRollColor;
                scale = highRollScale;
                break;
            case ERollTier.Whiff:
                label = whiffSuffix;
                color = lowRollColor;
                scale = lowRollScale;
                break;
            default:
                Destroy(gameObject);
                return;
        }

        sb.Clear();
        sb.Append(label);
        sb.Replace("\\n", "\n");
        string trimmed = sb.ToString().Trim();
        sb.Clear();
        sb.Append(trimmed);
        rainbowStartIndex = tier == ERollTier.Jackpot && rainbowJackpot ? 0 : -1;

        Begin(color, scale, normalStyle.material);
    }

    public void ShowDodge(bool isPerfect = false)
    {
        DamageStyle style = isPerfect ? perfectDodgeStyle : dodgeStyle;

        sb.Clear();
        sb.Append(style.format);
        rainbowStartIndex = -1;

        Begin(style.color, style.scale, style.material);
    }

    private void Begin(Color color, float scale, Material material)
    {
        sb.Replace("\\n", "\n");

        Material targetMaterial = GetGlowMaterial(material != null ? material : defaultMaterial);
        if (targetMaterial != null && text.fontSharedMaterial != targetMaterial)
            text.fontSharedMaterial = targetMaterial;

        text.text = sb.ToString();
        baseColor = color;
        baseScale = scale;
        velocity = new Vector3(riseVelocity.x + Random.Range(-randomSpreadX, randomSpreadX), riseVelocity.y, 0f);
        age = 0f;

        Apply(0f);
    }

    private Material GetGlowMaterial(Material baseMaterial)
    {
        if (baseMaterial == null || Mathf.Approximately(glowIntensity, 1f)) return baseMaterial;
        if (!baseMaterial.HasProperty(ShaderUtilities.ID_FaceColor)) return baseMaterial;

        var key = (baseMaterial, glowIntensity);
        if (glowMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;

        Material glow = new Material(baseMaterial);
        glow.name = $"{baseMaterial.name} (Glow {glowIntensity:0.##})";
        Color face = baseMaterial.GetColor(ShaderUtilities.ID_FaceColor);
        glow.SetColor(ShaderUtilities.ID_FaceColor, new Color(face.r * glowIntensity, face.g * glowIntensity, face.b * glowIntensity, face.a));

        glowMaterials[key] = glow;
        return glow;
    }

    private Color GetRollColor(float quality)
    {
        return quality < 0.5f
            ? Color.Lerp(lowRollColor, midRollColor, quality / 0.5f)
            : Color.Lerp(midRollColor, highRollColor, (quality - 0.5f) / 0.5f);
    }

    private DamageStyle PickStyle(DamageInfo info)
    {
        switch (info.DamageType)
        {
            case EDamageType.Skill: return skillStyle;
            case EDamageType.Dot: return dotStyle;
            case EDamageType.Reflect: return reflectStyle;
            default: return info.IsCrit ? critStyle : normalStyle;
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.position += velocity * Time.deltaTime;

        float t = lifetime > 0f ? Mathf.Clamp01(age / lifetime) : 1f;
        Apply(t);

        if (age >= lifetime)
            Destroy(gameObject);
    }

    private void Apply(float t)
    {
        Color c = baseColor;
        c.a *= alphaOverLife.Evaluate(t);
        text.color = c;

        transform.localScale = initialScale * (baseScale * scaleOverLife.Evaluate(t));

        if (rainbowStartIndex >= 0)
            ApplyRainbow(c.a);
    }

    private void ApplyRainbow(float alpha)
    {
        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;
        byte a = (byte)(Mathf.Clamp01(alpha) * 255f);

        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ch = info.characterInfo[i];
            if (!ch.isVisible || ch.index < rainbowStartIndex) continue;

            float hue = Mathf.Repeat(age * rainbowSpeed + i * rainbowCharOffset, 1f);
            Color32 rainbow = Color.HSVToRGB(hue, 1f, 1f);
            rainbow.a = a;

            Color32[] colors = info.meshInfo[ch.materialReferenceIndex].colors32;
            int v = ch.vertexIndex;
            colors[v] = rainbow;
            colors[v + 1] = rainbow;
            colors[v + 2] = rainbow;
            colors[v + 3] = rainbow;
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }
}