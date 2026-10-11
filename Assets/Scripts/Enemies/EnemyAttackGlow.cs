using Enemies;
using UnityEngine;

public class EnemyAttackGlow : MonoBehaviour
{
    [System.Serializable]
    public class Style
    {
        public bool enabled = true;
        [ColorUsage(false, true)] public Color color = Color.white;
        [Range(0f, 10f)] public float intensity = 2f;
        [Range(0f, 1f)] public float checkerLow = 0.5f;
        [Range(1, 4)] public int checkerSize = 1;
    }

    [Header("References")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private MultiplyOutline multiplyOutline;
    [SerializeField] private ColorGlow colorGlow;
    [SerializeField] private VfxBox colorSource;
    [SerializeField] private bool useVfxBoxColors = true;

    [Header("Animation")]
    [SerializeField] private bool onlyDuringAttackAnimation = true;
    [SerializeField] private string[] attackStateNames = { "Attack" };
    [SerializeField] private bool stopAtClipEnd = true;

    [Header("Styles By Force")]
    [SerializeField] private Style lightGlow = new Style { color = Color.white };
    [SerializeField] private Style mediumGlow = new Style { color = new Color(1f, 0.8f, 0.2f) };
    [SerializeField] private Style heavyGlow = new Style { color = new Color(1f, 0.15f, 0.1f), intensity = 3f };
    [SerializeField] private Style zeroForceGlow = new Style { enabled = false };

    [Header("Parry Window Boost")]
    [SerializeField, Min(0f)] private float parryIntensityBoost = 2f;
    [SerializeField, Min(0f)] private float parryBoostFadeSpeed = 30f;
    [SerializeField] private bool heavyGetsParryBoost = false;

    private readonly MultiplyOutline.GlowStyle outlineStyle = new MultiplyOutline.GlowStyle();
    private readonly ColorGlow.GlowStyle colorGlowStyle = new ColorGlow.GlowStyle();
    private bool glowOn;
    private bool hasApplied;
    private float boost;

    private void Awake()
    {
        if (enemy == null) enemy = GetComponentInParent<Enemy>();
        if (multiplyOutline == null) multiplyOutline = GetComponentInChildren<MultiplyOutline>(true);
        if (colorGlow == null && multiplyOutline == null) colorGlow = GetComponentInChildren<ColorGlow>(true);
        if (colorSource == null)
        {
            Component root = enemy != null ? (Component)enemy : this;
            colorSource = root.GetComponentInChildren<VfxBox>(true);
        }
    }

    private void OnDisable()
    {
        SetGlow(false);
        hasApplied = false;
        boost = 0f;
    }

    private void LateUpdate()
    {
        Style style = null;
        bool parryOpen = false;
        Color color = Color.white;

        if (enemy != null && enemy.Context != null && enemy.IsAttacking && InAttackAnimation(enemy.Context.Animator))
        {
            EnemyContext ctx = enemy.Context;
            style = ForceStyle(ctx.CurrentAttackForce);
            if (style != null)
                color = useVfxBoxColors && colorSource != null ? colorSource.GetColorForForce(ctx.CurrentAttackForce) : style.color;
            bool canBoost = ctx.CurrentAttackForce != AttackForce.Heavy || heavyGetsParryBoost;
            parryOpen = canBoost && ctx.AttackEvents != null && ctx.AttackEvents.ParryWindowOpen;
        }

        float targetBoost = parryOpen ? parryIntensityBoost : 0f;
        boost = parryBoostFadeSpeed <= 0f
            ? targetBoost
            : Mathf.MoveTowards(boost, targetBoost, parryBoostFadeSpeed * Time.deltaTime);

        bool on = style != null && style.enabled;
        if (on)
        {
            float intensity = Mathf.Min(10f, style.intensity + boost);

            outlineStyle.enabled = true;
            outlineStyle.color = color;
            outlineStyle.intensity = intensity;
            outlineStyle.checkerLow = style.checkerLow;
            outlineStyle.checkerSize = style.checkerSize;

            colorGlowStyle.enabled = true;
            colorGlowStyle.color = color;
            colorGlowStyle.intensity = intensity;
            colorGlowStyle.checkerLow = style.checkerLow;
            colorGlowStyle.checkerSize = style.checkerSize;
        }

        SetGlow(on);
    }

    private bool InAttackAnimation(Animator animator)
    {
        if (!onlyDuringAttackAnimation) return true;
        if (animator == null || attackStateNames == null || attackStateNames.Length == 0) return false;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        bool matches = false;
        foreach (string stateName in attackStateNames)
        {
            if (!string.IsNullOrEmpty(stateName) && state.IsName(stateName))
            {
                matches = true;
                break;
            }
        }

        if (!matches) return false;
        return !stopAtClipEnd || state.loop || state.normalizedTime < 1f;
    }

    private Style ForceStyle(AttackForce force)
    {
        switch (force)
        {
            case AttackForce.Heavy: return heavyGlow;
            case AttackForce.Medium: return mediumGlow;
            case AttackForce.Light: return lightGlow;
            default: return zeroForceGlow;
        }
    }

    private void SetGlow(bool on)
    {
        if (hasApplied && on == glowOn) return;

        hasApplied = true;
        glowOn = on;

        if (multiplyOutline != null) multiplyOutline.SetColorGlow(on ? outlineStyle : null);

        if (colorGlow != null)
        {
            if (on) colorGlow.SetStyle(colorGlowStyle);
            else colorGlow.GlowOff();
        }
    }
}