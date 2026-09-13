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
        [SerializeField] private float blinkSpeed = 15f;
        [SerializeField] private float minAlpha = 0.15f;
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
            if (stats != null) stats.OnHealthChanged -= HandleHealthChanged;

            if (combat != null)
            {
                combat.OnEnergyChanged -= HandleEnergyChanged;
                combat.OnEnergyChanged -= HandleMeterOverlay;
            }

            if (equipment != null) equipment.OnEquipmentChanged -= HandleEquipmentChanged;

            stats = null;
            combat = null;
            equipment = null;
        }

        private void Awake() => ResetGlowState();
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
                }
            }

            if (isReadyToUse && readyGlowImage != null && readyGlowImage.enabled)
            {
                Color c = readyGlowImage.color;
                c.a = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f);
                readyGlowImage.color = c;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (healthFillImage != null) healthFillImage.fillAmount = max > 0f ? current / max : 0f;
        }

        private void HandleEnergyChanged(float current, float max)
        {
            if (energyFillImage != null) energyFillImage.fillAmount = max > 0f ? current / max : 0f;
        }

        private void HandleMeterOverlay(float currentEnergy, float maxEnergy)
        {
            float normalized = maxEnergy > 0f ? currentEnergy / maxEnergy : 0f;
            if (meterOverlayImage != null) meterOverlayImage.fillAmount = 1f - normalized;
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
        }
    }
}