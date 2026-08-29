using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("UI Canvas References")]
    [SerializeField] private GameObject pauseCanvas;
    
    [Header("Player UI References")]
    [SerializeField] private PlayerHealthBar healthBar;
    [SerializeField] private PlayerEnergyBar energyBar;
    [SerializeField] private PlayerSkillIcon skillIcon;

    private UIWindowAnimator pauseAnimator;

    public GameObject PauseCanvas => pauseCanvas;
    public PlayerHealthBar HealthBar => healthBar;
    public PlayerEnergyBar EnergyBar => energyBar;
    public PlayerSkillIcon SkillIcon => skillIcon;

    private void Awake()
    {
        if (pauseCanvas != null)
        {
            pauseAnimator = pauseCanvas.GetComponentInChildren<UIWindowAnimator>();

            if (pauseAnimator != null)
            {
                pauseAnimator.InstantHide();
            }
            else
            {
                pauseCanvas.SetActive(false);
            }
        }
    }

    public void InitializePlayerUI(GameObject playerObject)
    {
        if (playerObject == null) return;

        if (healthBar != null && playerObject.TryGetComponent(out PlayerStats health))
        {
            healthBar.BindStats(health);
        }

        if (energyBar != null && playerObject.TryGetComponent(out PlayerCombatController energy))
        {
            energyBar.BindCombat(energy);
        }

        if (skillIcon != null)
        {
            playerObject.TryGetComponent(out PlayerEquipmentManager equipment);
            playerObject.TryGetComponent(out PlayerCombatController skill);
            
            skillIcon.Bind(equipment, skill);
        }
    }

    public void SetPauseCanvasActive(bool state)
    {
        if (pauseCanvas == null)
        {
            Debug.LogWarning("[UIManager] Pause Canvas reference is missing!");
            return;
        }

        if (pauseAnimator != null)
        {
            if (state)
            {
                pauseAnimator.Show(freezeplayer: false);
            }
            else
            {
                pauseAnimator.Hide();
            }
        }
        else
        {
            pauseCanvas.SetActive(state);
        }
    }

    public void TogglePauseCanvas()
    {
        if (pauseCanvas != null)
        {
            bool isCurrentlyActive = pauseCanvas.activeSelf;
            SetPauseCanvasActive(!isCurrentlyActive);
        }
    }
}