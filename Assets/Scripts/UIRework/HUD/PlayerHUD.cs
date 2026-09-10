using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class PlayerHUD : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Image healthFillImage;

        [Header("Energy")]
        [SerializeField] private Image energyFillImage;

        [Header("Skill Icon")]
        [SerializeField] private Image skillIconImage;
        [SerializeField] private Image meterOverlayImage;
        [SerializeField] private Image readyGlowImage;
        [SerializeField] private Sprite emptySlotSprite;

        [Header("Skill Icon Blink Settings")]
        [SerializeField] private float blinkSpeed = 4f;
        [SerializeField] private float minAlpha = 0.2f;
        [SerializeField] private float maxAlpha = 1f;

        private PlayerStats stats;
        private PlayerCombatController combat;
        private PlayerEquipmentManager equipment;
        private bool isReadyToUse;

        public void Bind(PlayerStats playerStats, PlayerCombatController playerCombat, PlayerEquipmentManager playerEquipment)
        {
            Unbind();

            stats = playerStats;
            combat = playerCombat;
            equipment = playerEquipment;

            ResetGlowState();

            if (stats != null)
            {
                stats.OnHealthChanged += HandleHealthChanged;
                HandleHealthChanged(stats.CurrentHealth, stats.MaxHealth);
            }

            if (combat != null)
            {
                combat.OnEnergyChanged += HandleEnergyChanged;
                combat.OnEnergyChanged += HandleMeterOverlay;
                HandleEnergyChanged(combat.SkillMeter, 1f);
                HandleMeterOverlay(combat.SkillMeter, 1f);
            }

            if (equipment != null)
            {
                equipment.OnEquipmentChanged += HandleEquipmentChanged;
                HandleEquipmentChanged();
            }
        }

        public void Unbind()
        {
            if (stats != null) { stats.OnHealthChanged -= HandleHealthChanged; }

            if (combat != null)
            {
                combat.OnEnergyChanged -= HandleEnergyChanged;
                combat.OnEnergyChanged -= HandleMeterOverlay;
            }

            if (equipment != null) { equipment.OnEquipmentChanged -= HandleEquipmentChanged; }

            stats = null;
            combat = null;
            equipment = null;
        }

        private void Awake()
        {
            ResetGlowState();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            if (isReadyToUse && readyGlowImage != null)
            {
                if (!readyGlowImage.enabled) { readyGlowImage.enabled = true; }

                float wave = (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f;
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);

                Color c = readyGlowImage.color;
                c.a = alpha;
                readyGlowImage.color = c;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (healthFillImage == null || max <= 0f) return;
            healthFillImage.fillAmount = current / max;
        }

        private void HandleEnergyChanged(float current, float max)
        {
            if (energyFillImage == null || max <= 0f) return;
            energyFillImage.fillAmount = current / max;
        }

        private void HandleMeterOverlay(float currentEnergy, float maxEnergy)
        {
            if (maxEnergy <= 0f || combat == null) return;

            float energyNormalized = Mathf.Clamp01(currentEnergy / combat.SkillActivationCost);

            if (meterOverlayImage != null)
            {
                meterOverlayImage.fillAmount = 1f - energyNormalized;
            }

            bool wasReady = isReadyToUse;
            isReadyToUse = energyNormalized >= 1f;

            if (wasReady && !isReadyToUse)
            {
                ResetGlowState();
            }
        }

        private void HandleEquipmentChanged()
        {
            if (skillIconImage == null) return;

            var specialDef = equipment != null ? equipment.SpecialAttackDef : null;

            if (specialDef != null && specialDef.UISprite != null)
            {
                skillIconImage.sprite = specialDef.UISprite;
                skillIconImage.enabled = true;
            }
            else if (emptySlotSprite != null)
            {
                skillIconImage.sprite = emptySlotSprite;
                skillIconImage.enabled = true;
            }
            else
            {
                skillIconImage.enabled = false;
            }
        }

        private void ResetGlowState()
        {
            if (readyGlowImage == null) return;

            Color c = readyGlowImage.color;
            c.a = 0f;
            readyGlowImage.color = c;
            readyGlowImage.enabled = false;
        }
    }
}