using UnityEngine;
using UnityEngine.UI;

public class PlayerSkillIcon : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image skillIconImage;
    [SerializeField] private Image meterOverlayImage;
    [SerializeField] private Image readyGlowImage;
    [SerializeField] private Sprite emptySlotSprite;

    [Header("Blink Settings")]
    [SerializeField] private float blinkSpeed = 4f;
    [SerializeField] private float minAlpha = 0.2f;
    [SerializeField] private float maxAlpha = 1f;

    private PlayerEquipmentManager equipmentManager;
    private PlayerCombatController combatController;
    private bool isReadyToUse;

    public void Bind(PlayerEquipmentManager equipment, PlayerCombatController combat)
    {
        if (equipmentManager != null)
        {
            equipmentManager.OnEquipmentChanged -= UpdateSkillIcon;
        }
        if (combatController != null)
        {
            combatController.OnEnergyChanged -= UpdateMeterOverlay;
        }

        equipmentManager = equipment;
        combatController = combat;

        ResetGlowState();

        if (equipmentManager != null)
        {
            equipmentManager.OnEquipmentChanged += UpdateSkillIcon;
            UpdateSkillIcon();
        }

        if (combatController != null)
        {
            combatController.OnEnergyChanged += UpdateMeterOverlay;
            UpdateMeterOverlay(combatController.SkillMeter, 1f);
        }
    }

    private void Awake()
    {
        ResetGlowState();
    }

    private void OnDestroy()
    {
        if (equipmentManager != null)
        {
            equipmentManager.OnEquipmentChanged -= UpdateSkillIcon;
        }
        if (combatController != null)
        {
            combatController.OnEnergyChanged -= UpdateMeterOverlay;
        }
    }

    private void Update()
    {
        if (isReadyToUse && readyGlowImage != null)
        {
            if (!readyGlowImage.enabled) 
                readyGlowImage.enabled = true;

            float wave = (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f; 
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave);

            Color c = readyGlowImage.color;
            c.a = alpha;
            readyGlowImage.color = c;
        }
    }

    private void UpdateSkillIcon()
    {
        if (skillIconImage == null) return;

        var specialDef = equipmentManager != null ? equipmentManager.SpecialAttackDef : null;

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

    private void UpdateMeterOverlay(float currentEnergy, float maxEnergy)
    {
        if (maxEnergy <= 0f) return;

        float energyNormalized = Mathf.Clamp01(currentEnergy / maxEnergy);

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

    private void ResetGlowState()
    {
        if (readyGlowImage == null) return;

        Color c = readyGlowImage.color;
        c.a = 0f;
        readyGlowImage.color = c;
        readyGlowImage.enabled = false;
    }
}