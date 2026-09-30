using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
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

        [Header("Health Chip")]
        [SerializeField] private Image healthChipImage;
        [SerializeField] private float chipDelay = 0.4f;
        [SerializeField] private float chipTweenDuration = 0.6f;

        [Header("Low Health Pulse")]
        [SerializeField] private float lowHealthThreshold = 0.25f;
        [SerializeField] private Color lowHealthPulseColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private float lowHealthPulseSpeed = 4f;

        [Header("Energy")]
        [SerializeField] private Image energyFillImage;
        [SerializeField] private float energyTweenDuration = 0.25f;

        [Header("Energy Feedback")]
        [SerializeField] private Color energyFlashColor = Color.white;
        [SerializeField] private float energyFlashDuration = 0.12f;
        [SerializeField] private Color energyFullPulseColor = new Color(0.4f, 0.9f, 1f);
        [SerializeField] private float energyFullPulseSpeed = 3f;

        [Header("Heals")]
        [SerializeField] private Image healImage;
        [SerializeField] private Sprite[] healSprites;

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

        private PlayerStats stats;
        private PlayerCombatController combat;
        private PlayerEquipmentManager equipment;
        private PlayerHeals heals;
        private bool isReadyToUse;
        private bool isLowHealth;

        private Coroutine healthTweenRoutine;
        private Coroutine healthFlashRoutine;
        private Coroutine chipRoutine;
        private Coroutine energyTweenRoutine;
        private Coroutine energyFlashRoutine;
        private Coroutine readyPunchRoutine;
        private Color healthBaseColor;
        private Color energyBaseColor;
        private Vector3 skillIconBaseScale = Vector3.one;
        private bool isEnergyFull;

        public void Bind(PlayerStats playerStats, PlayerCombatController playerCombat, PlayerEquipmentManager playerEquipment, PlayerHeals playerHeals)
        {
            Unbind();
            stats = playerStats;
            combat = playerCombat;
            equipment = playerEquipment;
            heals = playerHeals;

            ResetGlowState();

            if (stats != null)
            {
                stats.OnHealthChanged += HandleHealthChanged;
                SnapHealth(stats.CurrentHealth, stats.MaxHealth);
            }

            if (combat != null)
            {
                combat.OnEnergyChanged += HandleEnergyChanged;
                SnapEnergy(combat.SkillMeter, 1f);
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

            if (combat != null)
            {
                combat.OnEnergyChanged -= HandleEnergyChanged;
            }

            if (equipment != null) equipment.OnEquipmentChanged -= HandleEquipmentChanged;

            stats = null;
            combat = null;
            equipment = null;
        }

        private void Awake()
        {
            if (skillIconImage != null) skillIconBaseScale = skillIconImage.transform.localScale;
            if (healthFillImage != null) healthBaseColor = healthFillImage.color;
            if (energyFillImage != null) energyBaseColor = energyFillImage.color;

            ResetGlowState();
        }

        private void OnDestroy() => Unbind();

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

            UpdateLowHealthPulse();
            UpdateEnergyFullPulse();
        }

        private void UpdateLowHealthPulse()
        {
            if (healthFillImage == null) return;

            bool lowHealth = healthFillImage.fillAmount > 0f && healthFillImage.fillAmount <= lowHealthThreshold;

            if (lowHealth != isLowHealth)
            {
                isLowHealth = lowHealth;
                if (!isLowHealth && healthFlashRoutine == null)
                {
                    healthFillImage.color = healthBaseColor;
                }
            }

            if (isLowHealth && healthFlashRoutine == null)
            {
                float pulseT = (Mathf.Sin(Time.unscaledTime * lowHealthPulseSpeed) + 1f) * 0.5f;
                healthFillImage.color = Color.Lerp(healthBaseColor, lowHealthPulseColor, pulseT);
            }
        }

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

        private void HandleHealthChanged(float current, float max)
        {
            float target = max > 0f ? current / max : 0f;
            float displayed = healthFillImage != null ? healthFillImage.fillAmount : target;

            if (target < displayed)
            {
                TriggerHealthFlash();
                StartChipDrain(displayed, target);
            }
            else
            {
                if (chipRoutine != null) StopCoroutine(chipRoutine);
                chipRoutine = null;
                if (healthChipImage != null) healthChipImage.fillAmount = target;
            }

            if (healthTweenRoutine != null) StopCoroutine(healthTweenRoutine);
            healthTweenRoutine = StartCoroutine(TweenFill(healthFillImage, target, healthTweenDuration));
        }

        private void HandleEnergyChanged(float current, float max)
        {
            float target = max > 0f ? current / max : 0f;
            float displayed = energyFillImage != null ? energyFillImage.fillAmount : target;

            if (target < displayed)
            {
                TriggerEnergyFlash();
            }

            if (energyTweenRoutine != null) StopCoroutine(energyTweenRoutine);
            energyTweenRoutine = StartCoroutine(TweenEnergy(target, energyTweenDuration));
        }

        private void UpdateEnergyFullPulse()
        {
            if (energyFillImage == null) return;

            bool full = energyFillImage.fillAmount >= 0.999f;

            if (full != isEnergyFull)
            {
                isEnergyFull = full;
                if (!isEnergyFull && energyFlashRoutine == null)
                {
                    energyFillImage.color = energyBaseColor;
                }
            }

            if (isEnergyFull && energyFlashRoutine == null)
            {
                float pulseT = (Mathf.Sin(Time.unscaledTime * energyFullPulseSpeed) + 1f) * 0.5f;
                energyFillImage.color = Color.Lerp(energyBaseColor, energyFullPulseColor, pulseT);
            }
        }

        private void TriggerEnergyFlash()
        {
            if (energyFillImage == null) return;

            if (energyFlashRoutine != null) StopCoroutine(energyFlashRoutine);
            energyFlashRoutine = StartCoroutine(FlashEnergyColor());
        }

        private IEnumerator FlashEnergyColor()
        {
            energyFillImage.color = energyFlashColor;

            float t = 0f;
            while (t < energyFlashDuration)
            {
                t += Time.unscaledDeltaTime;
                energyFillImage.color = Color.Lerp(energyFlashColor, energyBaseColor, Mathf.Clamp01(t / energyFlashDuration));
                yield return null;
            }

            energyFillImage.color = energyBaseColor;
            energyFlashRoutine = null;
        }

        private void TriggerHealthFlash()
        {
            if (healthFillImage == null) return;

            if (healthFlashRoutine != null) StopCoroutine(healthFlashRoutine);
            healthFlashRoutine = StartCoroutine(FlashHealthColor());
        }

        private IEnumerator FlashHealthColor()
        {
            healthFillImage.color = healthFlashColor;

            float t = 0f;
            while (t < healthFlashDuration)
            {
                t += Time.unscaledDeltaTime;
                healthFillImage.color = Color.Lerp(healthFlashColor, healthBaseColor, Mathf.Clamp01(t / healthFlashDuration));
                yield return null;
            }

            healthFillImage.color = healthBaseColor;
            healthFlashRoutine = null;
        }

        private void StartChipDrain(float fromValue, float toValue)
        {
            if (healthChipImage == null) return;

            healthChipImage.fillAmount = Mathf.Max(healthChipImage.fillAmount, fromValue);

            if (chipRoutine != null) StopCoroutine(chipRoutine);
            chipRoutine = StartCoroutine(ChipDrainRoutine(toValue));
        }

        private IEnumerator ChipDrainRoutine(float target)
        {
            float t = 0f;
            while (t < chipDelay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            float start = healthChipImage.fillAmount;
            t = 0f;
            while (t < chipTweenDuration)
            {
                t += Time.unscaledDeltaTime;
                healthChipImage.fillAmount = Mathf.Lerp(start, target, Mathf.Clamp01(t / chipTweenDuration));
                yield return null;
            }

            healthChipImage.fillAmount = target;
            chipRoutine = null;
        }

        private IEnumerator TweenFill(Image image, float target, float duration)
        {
            if (image == null) yield break;

            float start = image.fillAmount;

            if (duration <= 0f)
            {
                image.fillAmount = target;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                image.fillAmount = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }

            image.fillAmount = target;
        }

        private IEnumerator TweenEnergy(float target, float duration)
        {
            float start = energyFillImage != null ? energyFillImage.fillAmount : target;

            if (duration <= 0f)
            {
                ApplyEnergy(target);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                ApplyEnergy(Mathf.Lerp(start, target, Mathf.Clamp01(t / duration)));
                yield return null;
            }

            ApplyEnergy(target);
        }

        private void ApplyEnergy(float normalized)
        {
            if (energyFillImage != null) energyFillImage.fillAmount = normalized;
            if (meterOverlayImage != null) meterOverlayImage.fillAmount = 1f - normalized;
        }

        private void SnapHealth(float current, float max)
        {
            if (healthTweenRoutine != null) StopCoroutine(healthTweenRoutine);
            if (healthFlashRoutine != null) StopCoroutine(healthFlashRoutine);
            if (chipRoutine != null) StopCoroutine(chipRoutine);
            chipRoutine = null;
            isLowHealth = false;

            float value = max > 0f ? current / max : 0f;

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = value;
                healthFillImage.color = healthBaseColor;
            }

            if (healthChipImage != null) healthChipImage.fillAmount = value;
        }

        private void SnapEnergy(float current, float max)
        {
            if (energyTweenRoutine != null) StopCoroutine(energyTweenRoutine);
            if (energyFlashRoutine != null) StopCoroutine(energyFlashRoutine);
            isEnergyFull = false;

            ApplyEnergy(max > 0f ? current / max : 0f);

            if (energyFillImage != null) energyFillImage.color = energyBaseColor;
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

        private void UpdateHealUI(int healCount)
        {
            healImage.sprite = healCount switch
            {
                0 => healSprites[0],
                1 => healSprites[1],
                2 => healSprites[2],
                3 => healSprites[3],
                _ => healImage.sprite
            };
        }
    }
}