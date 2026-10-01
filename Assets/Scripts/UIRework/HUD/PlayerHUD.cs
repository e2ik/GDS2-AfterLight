using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Image healthFillImage;
        [SerializeField] private float healthTweenDuration = 0.35f;
        [SerializeField] private Color healthFlashColor = Color.white;
        [SerializeField] private float healthFlashDuration = 0.15f;
        [SerializeField] private Color healthGainFlashColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField, Min(0f)] private float healthGlowIntensity = 1f;
        [SerializeField, Min(0f)] private float healthFlashIntensity = 2f;

        [Header("Health Chip")]
        [SerializeField] private Image healthChipImage;
        [SerializeField] private float chipDelay = 0.4f;
        [SerializeField] private float chipTweenDuration = 0.6f;
        [SerializeField] private Color healthChipDamageColor = new Color(1f, 0.85f, 0.85f);
        [SerializeField] private Color healthChipHealColor = new Color(0.45f, 1f, 0.45f);

        [Header("Healing")]
        [SerializeField] private Color healthRisingColor = new Color(0.4f, 1f, 0.4f);
        [SerializeField] private bool showHealCasting = true;
        [SerializeField] private Color healCastingColor = new Color(0.7f, 1f, 0.7f);
        [SerializeField] private float healCastingPulseSpeed = 8f;

        [Header("Low Health Pulse")]
        [SerializeField] private float lowHealthThreshold = 0.25f;
        [SerializeField] private Color lowHealthPulseColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private float lowHealthPulseSpeed = 4f;

        [Header("Energy")]
        [SerializeField] private Image energyFillImage;
        [SerializeField] private float energyTweenDuration = 0.25f;
        [SerializeField, Min(0f)] private float energyGlowIntensity = 1f;
        [SerializeField, Min(0f)] private float energyFlashIntensity = 2f;

        [Header("Energy Chip")]
        [SerializeField] private Image energyChipImage;
        [SerializeField] private float energyChipDelay = 0.3f;
        [SerializeField] private float energyChipTweenDuration = 0.4f;
        [SerializeField] private Color energyChipSpendColor = new Color(1f, 0.6f, 0.3f);
        [SerializeField] private Color energyChipGainColor = new Color(0.6f, 1f, 1f);

        [Header("Energy Feedback")]
        [SerializeField] private Color energyFlashColor = Color.white;
        [SerializeField] private Color energyGainFlashColor = new Color(0.6f, 1f, 1f);
        [SerializeField] private float energyFlashDuration = 0.12f;
        [SerializeField] private float energyFlashMinChange = 0.02f;
        [SerializeField] private Color energyFullPulseColor = new Color(0.4f, 0.9f, 1f);
        [SerializeField] private float energyFullPulseSpeed = 10f;

        [Header("Heals")]
        [SerializeField] private Image healImage;
        [SerializeField] private Sprite[] healSprites;

        [Header("Heal Icon Feedback")]
        [SerializeField] private Color healDeniedColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private float healDeniedDuration = 0.4f;
        [SerializeField] private int healDeniedPulses = 2;
        [SerializeField] private float healShakeStrength = 6f;
        [SerializeField] private float healShakeFrequency = 40f;
        [SerializeField] private Color healActivatedColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private float healActivatedDuration = 0.3f;
        [SerializeField] private float healActivatedScale = 1.2f;

        [Header("Skill Icon")]
        [SerializeField] private Image skillIconImage;
        [SerializeField] private Image meterOverlayImage;
        [SerializeField] private Image readyGlowImage;
        [SerializeField] private Sprite emptySlotSprite;

        [Header("Skill Icon Blink Settings")]
        [SerializeField] private float blinkSpeed = 15f;
        [SerializeField] private float minAlpha = 0.15f;
        [SerializeField] private float maxAlpha = 1f;

        [Header("Skill Icon Ready Punch")]
        [SerializeField] private float readyPunchScale = 1.2f;
        [SerializeField] private float readyPunchDuration = 0.25f;

        private class BarState
        {
            public Image fill;
            public Image chip;
            public Material fillMaterial;
            public Material chipMaterial;

            public float fillStart;
            public float fillTarget;
            public float fillElapsed;
            public float fillDuration;

            public float chipHold;
            public float chipStart;
            public float chipElapsed;
            public bool chipDraining;

            public float flashTimer;
            public float flashDuration;
            public Color flashColor;
        }

        private static readonly int TintId = Shader.PropertyToID("_Color");

        private PlayerStats stats;
        private PlayerCombatController combat;
        private PlayerEquipmentManager equipment;
        private PlayerHeals heals;
        private PlayerController controller;
        private bool isReadyToUse;

        private readonly BarState healthBar = new BarState();
        private readonly BarState energyBar = new BarState();

        private Coroutine readyPunchRoutine;
        private Coroutine healIconRoutine;
        private Color healIconBaseColor = Color.white;
        private Vector3 healIconBasePosition;
        private Vector3 healIconBaseScale = Vector3.one;
        private Color healthBaseColor;
        private Color energyBaseColor;
        private Vector3 skillIconBaseScale = Vector3.one;

        public void Bind(PlayerStats playerStats, PlayerCombatController playerCombat, PlayerEquipmentManager playerEquipment, PlayerHeals playerHeals)
        {
            Unbind();
            stats = playerStats;
            combat = playerCombat;
            equipment = playerEquipment;
            heals = playerHeals;
            controller = stats != null ? stats.GetComponent<PlayerController>() : null;

            if (controller != null)
            {
                controller.OnHealDenied += HandleHealDenied;
                controller.OnHealStarted += HandleHealStarted;
            }

            ResetGlowState();

            if (stats != null)
            {
                stats.OnHealthChanged += HandleHealthChanged;
                SnapBar(healthBar, stats.MaxHealth > 0f ? stats.CurrentHealth / stats.MaxHealth : 0f);
            }

            if (combat != null)
            {
                combat.OnEnergyChanged += HandleEnergyChanged;
                SnapBar(energyBar, combat.SkillMeter);
                ApplyEnergyOverlay(combat.SkillMeter);
            }

            if (equipment != null)
            {
                equipment.OnEquipmentChanged += HandleEquipmentChanged;
                HandleEquipmentChanged();
            }

            if (heals != null)
            {
                heals.OnUpdateHeals += UpdateHealUI;
                UpdateHealUI(heals.GetCurrentHealCount());
            }
        }

        public void Unbind()
        {
            if (stats != null) stats.OnHealthChanged -= HandleHealthChanged;
            if (combat != null) combat.OnEnergyChanged -= HandleEnergyChanged;
            if (equipment != null) equipment.OnEquipmentChanged -= HandleEquipmentChanged;
            if (heals != null) heals.OnUpdateHeals -= UpdateHealUI;
            if (controller != null)
            {
                controller.OnHealDenied -= HandleHealDenied;
                controller.OnHealStarted -= HandleHealStarted;
            }

            stats = null;
            combat = null;
            equipment = null;
            heals = null;
            controller = null;
        }

        private void Awake()
        {
            if (skillIconImage != null) skillIconBaseScale = skillIconImage.transform.localScale;
            if (healthFillImage != null) healthBaseColor = healthFillImage.color;
            if (energyFillImage != null) energyBaseColor = energyFillImage.color;
            if (healImage != null)
            {
                healIconBaseColor = healImage.color;
                healIconBasePosition = healImage.rectTransform.anchoredPosition3D;
                healIconBaseScale = healImage.rectTransform.localScale;
            }

            SetupBar(healthBar, healthFillImage, healthChipImage);
            SetupBar(energyBar, energyFillImage, energyChipImage);

            ResetGlowState();
        }

        private void OnDestroy()
        {
            Unbind();
            DestroyBarMaterials(healthBar);
            DestroyBarMaterials(energyBar);
        }

        private void Update()
        {
            if (combat != null)
            {
                bool ready = combat.IsSkillReady;
                if (ready != isReadyToUse)
                {
                    isReadyToUse = ready;
                    if (readyGlowImage != null) readyGlowImage.enabled = ready;
                    if (!ready) ResetGlowState();
                    else TriggerReadyPunch();
                }
            }

            if (isReadyToUse && readyGlowImage != null && readyGlowImage.enabled)
            {
                Color c = readyGlowImage.color;
                c.a = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f);
                readyGlowImage.color = c;
            }

            float dt = Time.unscaledDeltaTime;

            UpdateBar(healthBar, dt, chipDelay, chipTweenDuration);
            UpdateBar(energyBar, dt, energyChipDelay, energyChipTweenDuration);
            if (energyBar.fill != null) ApplyEnergyOverlay(energyBar.fill.fillAmount);

            UpdateHealthColor();
            UpdateEnergyColor();
        }

        #region Bars

        private void SetupBar(BarState bar, Image fill, Image chip)
        {
            bar.fill = fill;
            bar.chip = chip;
            bar.fillMaterial = CreateTintMaterial(fill);
            bar.chipMaterial = CreateTintMaterial(chip);
        }

        private static Material CreateTintMaterial(Image image)
        {
            if (image == null) return null;

            Material source = image.material != null ? image.material : Graphic.defaultGraphicMaterial;
            Material instance = new Material(source) { name = $"{source.name} ({image.name})" };
            image.material = instance;
            return instance;
        }

        private static void DestroyBarMaterials(BarState bar)
        {
            if (bar.fillMaterial != null) Destroy(bar.fillMaterial);
            if (bar.chipMaterial != null) Destroy(bar.chipMaterial);
        }

        private static void SetIntensity(Material material, float intensity)
        {
            if (material == null || !material.HasProperty(TintId)) return;
            material.SetColor(TintId, new Color(intensity, intensity, intensity, 1f));
        }

        private void SnapBar(BarState bar, float value)
        {
            bar.fillStart = bar.fillTarget = value;
            bar.fillElapsed = bar.fillDuration = 0f;
            bar.chipDraining = false;
            bar.chipHold = 0f;
            bar.flashTimer = 0f;

            if (bar.fill != null) bar.fill.fillAmount = value;
            if (bar.chip != null) bar.chip.fillAmount = value;
        }

        private void ChangeBar(BarState bar, float target, float tweenDuration, float delay, Color loseChipColor, Color gainChipColor)
        {
            float displayed = bar.fill != null ? bar.fill.fillAmount : target;

            bar.fillStart = displayed;
            bar.fillTarget = target;
            bar.fillElapsed = 0f;
            bar.fillDuration = tweenDuration;

            if (bar.chip == null) return;

            if (target < displayed)
            {
                bar.chip.color = loseChipColor;
                bar.chip.fillAmount = Mathf.Max(bar.chip.fillAmount, displayed);
                bar.chipHold = delay;
                bar.chipDraining = false;
            }
            else if (target > displayed)
            {
                bar.chip.color = gainChipColor;
                bar.chip.fillAmount = target;
                bar.chipDraining = false;
                bar.chipHold = 0f;
            }
        }

        private void UpdateBar(BarState bar, float dt, float delay, float chipDuration)
        {
            if (bar.fill != null)
            {
                if (bar.fillDuration > 0f && bar.fillElapsed < bar.fillDuration)
                {
                    bar.fillElapsed += dt;
                    bar.fill.fillAmount = Mathf.Lerp(bar.fillStart, bar.fillTarget, Mathf.Clamp01(bar.fillElapsed / bar.fillDuration));
                }
                else
                {
                    bar.fill.fillAmount = bar.fillTarget;
                }
            }

            if (bar.chip != null)
            {
                float target = bar.fillTarget;

                if (bar.chip.fillAmount > target + 0.0001f)
                {
                    if (bar.chipHold > 0f)
                    {
                        bar.chipHold -= dt;
                    }
                    else
                    {
                        if (!bar.chipDraining)
                        {
                            bar.chipDraining = true;
                            bar.chipStart = bar.chip.fillAmount;
                            bar.chipElapsed = 0f;
                        }

                        bar.chipElapsed += dt;
                        float t = chipDuration > 0f ? Mathf.Clamp01(bar.chipElapsed / chipDuration) : 1f;
                        bar.chip.fillAmount = Mathf.Lerp(bar.chipStart, target, t);
                    }
                }
                else
                {
                    bar.chipDraining = false;
                    if (bar.fill != null && bar.chip.fillAmount < bar.fill.fillAmount)
                        bar.chip.fillAmount = bar.fill.fillAmount;
                }
            }

            if (bar.flashTimer > 0f) bar.flashTimer = Mathf.Max(0f, bar.flashTimer - dt);
        }

        private static void Flash(BarState bar, Color color, float duration)
        {
            bar.flashColor = color;
            bar.flashDuration = Mathf.Max(0.0001f, duration);
            bar.flashTimer = bar.flashDuration;
        }

        private static float FlashAmount(BarState bar)
        {
            return bar.flashTimer > 0f ? bar.flashTimer / bar.flashDuration : 0f;
        }

        #endregion

        #region Health

        private void HandleHealthChanged(float current, float max)
        {
            float target = max > 0f ? current / max : 0f;
            float displayed = healthBar.fill != null ? healthBar.fill.fillAmount : target;

            if (target < displayed) Flash(healthBar, healthFlashColor, healthFlashDuration);
            else if (target > displayed) Flash(healthBar, healthGainFlashColor, healthFlashDuration);

            ChangeBar(healthBar, target, healthTweenDuration, chipDelay, healthChipDamageColor, healthChipHealColor);
        }

        private void UpdateHealthColor()
        {
            if (healthBar.fill == null) return;

            Color baseColor = healthBaseColor;
            float fill = healthBar.fill.fillAmount;

            if (fill < healthBar.fillTarget - 0.0001f)
            {
                baseColor = healthRisingColor;
            }
            else if (showHealCasting && controller != null && controller.IsHealing)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * healCastingPulseSpeed) + 1f) * 0.5f;
                baseColor = Color.Lerp(healthBaseColor, healCastingColor, pulse);
            }
            else if (fill > 0f && fill <= lowHealthThreshold)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * lowHealthPulseSpeed) + 1f) * 0.5f;
                baseColor = Color.Lerp(healthBaseColor, lowHealthPulseColor, pulse);
            }

            float flash = FlashAmount(healthBar);
            healthBar.fill.color = Color.Lerp(baseColor, healthBar.flashColor, flash);
            SetIntensity(healthBar.fillMaterial, Mathf.Lerp(healthGlowIntensity, healthFlashIntensity, flash));
            SetIntensity(healthBar.chipMaterial, healthGlowIntensity);
        }

        #endregion

        #region Energy

        private void HandleEnergyChanged(float current, float max)
        {
            float target = max > 0f ? current / max : 0f;
            float displayed = energyBar.fill != null ? energyBar.fill.fillAmount : target;
            float change = target - energyBar.fillTarget;

            if (change <= -energyFlashMinChange) Flash(energyBar, energyFlashColor, energyFlashDuration);
            else if (change >= energyFlashMinChange) Flash(energyBar, energyGainFlashColor, energyFlashDuration);

            if (Mathf.Abs(target - displayed) < 0.0001f && Mathf.Abs(change) < 0.0001f) return;

            ChangeBar(energyBar, target, energyTweenDuration, energyChipDelay, energyChipSpendColor, energyChipGainColor);
        }

        private void UpdateEnergyColor()
        {
            if (energyBar.fill == null) return;

            Color baseColor = energyBaseColor;

            if (energyBar.fill.fillAmount >= 0.999f)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * energyFullPulseSpeed) + 1f) * 0.5f;
                baseColor = Color.Lerp(energyBaseColor, energyFullPulseColor, pulse);
            }

            float flash = FlashAmount(energyBar);
            energyBar.fill.color = Color.Lerp(baseColor, energyBar.flashColor, flash);
            SetIntensity(energyBar.fillMaterial, Mathf.Lerp(energyGlowIntensity, energyFlashIntensity, flash));
            SetIntensity(energyBar.chipMaterial, energyGlowIntensity);
        }

        private void ApplyEnergyOverlay(float normalized)
        {
            if (meterOverlayImage != null) meterOverlayImage.fillAmount = 1f - normalized;
        }

        #endregion

        #region Skill Icon

        private void TriggerReadyPunch()
        {
            if (skillIconImage == null) return;

            if (readyPunchRoutine != null) StopCoroutine(readyPunchRoutine);
            readyPunchRoutine = StartCoroutine(PunchSkillIcon());
        }

        private IEnumerator PunchSkillIcon()
        {
            Transform t = skillIconImage.transform;
            float riseDuration = readyPunchDuration * 0.35f;
            float fallDuration = readyPunchDuration - riseDuration;

            float elapsed = 0f;
            while (elapsed < riseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / riseDuration);
                t.localScale = Vector3.Lerp(skillIconBaseScale, skillIconBaseScale * readyPunchScale, p);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / fallDuration);
                t.localScale = Vector3.Lerp(skillIconBaseScale * readyPunchScale, skillIconBaseScale, p);
                yield return null;
            }

            t.localScale = skillIconBaseScale;
            readyPunchRoutine = null;
        }

        private void HandleEquipmentChanged()
        {
            if (skillIconImage == null) return;

            var specialDef = equipment != null ? equipment.SpecialAttackDef : null;
            skillIconImage.sprite = specialDef != null ? specialDef.UISprite : emptySlotSprite;
        }

        private void ResetGlowState()
        {
            isReadyToUse = false;
            if (readyGlowImage != null)
            {
                Color c = readyGlowImage.color;
                c.a = 0f;
                readyGlowImage.color = c;
                readyGlowImage.enabled = false;
            }

            if (readyPunchRoutine != null)
            {
                StopCoroutine(readyPunchRoutine);
                readyPunchRoutine = null;
            }

            if (skillIconImage != null) skillIconImage.transform.localScale = skillIconBaseScale;
        }

        #endregion

        private void HandleHealDenied()
        {
            if (healImage == null) return;
            if (healIconRoutine != null) StopCoroutine(healIconRoutine);
            healIconRoutine = StartCoroutine(HealDeniedRoutine());
        }

        private void HandleHealStarted()
        {
            if (healImage == null) return;
            if (healIconRoutine != null) StopCoroutine(healIconRoutine);
            healIconRoutine = StartCoroutine(HealActivatedRoutine());
        }

        private IEnumerator HealDeniedRoutine()
        {
            RectTransform rect = healImage.rectTransform;
            ResetHealIcon();

            float t = 0f;
            while (t < healDeniedDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / healDeniedDuration);
                float falloff = 1f - p;

                float shake = Mathf.Sin(t * healShakeFrequency) * healShakeStrength * falloff;
                rect.anchoredPosition3D = healIconBasePosition + new Vector3(shake, 0f, 0f);

                float pulse = Mathf.Abs(Mathf.Sin(p * Mathf.PI * Mathf.Max(1, healDeniedPulses)));
                healImage.color = Color.Lerp(healIconBaseColor, healDeniedColor, pulse);

                yield return null;
            }

            ResetHealIcon();
            healIconRoutine = null;
        }

        private IEnumerator HealActivatedRoutine()
        {
            RectTransform rect = healImage.rectTransform;
            ResetHealIcon();

            float t = 0f;
            while (t < healActivatedDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / healActivatedDuration);
                float bump = Mathf.Sin(p * Mathf.PI);

                rect.localScale = healIconBaseScale * Mathf.Lerp(1f, healActivatedScale, bump);
                healImage.color = Color.Lerp(healIconBaseColor, healActivatedColor, bump);

                yield return null;
            }

            ResetHealIcon();
            healIconRoutine = null;
        }

        private void ResetHealIcon()
        {
            if (healImage == null) return;
            healImage.rectTransform.anchoredPosition3D = healIconBasePosition;
            healImage.rectTransform.localScale = healIconBaseScale;
            healImage.color = healIconBaseColor;
        }

        private void UpdateHealUI(int healCount)
        {
            if (healImage == null || healSprites == null || healSprites.Length == 0) return;

            int index = Mathf.Clamp(healCount, 0, healSprites.Length - 1);
            healImage.sprite = healSprites[index];
        }
    }
}